﻿using System;
using System.Collections.Generic;
using System.Net;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Common.Serializer;

namespace NzbDrone.Core.Download.Clients.UTorrent
{
    public interface IUTorrentProxy
    {
        int GetVersion(UTorrentSettings settings);
        Dictionary<string, string> GetConfig(UTorrentSettings settings);
        UTorrentResponse GetTorrents(string cacheID, UTorrentSettings settings);

        void AddTorrentFromUrl(string torrentUrl, UTorrentSettings settings);
        void AddTorrentFromFile(string fileName, byte[] fileContent, UTorrentSettings settings);
        void SetTorrentSeedingConfiguration(string hash, TorrentSeedConfiguration seedConfiguration, UTorrentSettings settings);

        void RemoveTorrent(string hash, bool removeData, UTorrentSettings settings);
        void SetTorrentLabel(string hash, string label, UTorrentSettings settings);
        void MoveTorrentToTopInQueue(string hash, UTorrentSettings settings);
        void StartTorrent(string hash, UTorrentSettings settings);
        void StopTorrent(string hash, UTorrentSettings settings);
    }

    public class UTorrentProxy : IUTorrentProxy
    {
        private readonly IHttpClient _httpClient;
        private readonly Logger _logger;

        private readonly ICached<Dictionary<string, string>> _authCookieCache;
        private readonly ICached<string> _authTokenCache;

        public UTorrentProxy(ICacheManager cacheManager, IHttpClient httpClient, Logger logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            _authCookieCache = cacheManager.GetCache<Dictionary<string, string>>(GetType(), "authCookies");
            _authTokenCache = cacheManager.GetCache<string>(GetType(), "authTokens");
        }

        public int GetVersion(UTorrentSettings settings)
        {
            var requestBuilder = BuildRequest(settings)
                .AddQueryParam("action", "getsettings");

            var result = ProcessRequest(requestBuilder, settings);

            return result.Build;
        }

        public Dictionary<string, string> GetConfig(UTorrentSettings settings)
        {
            var requestBuilder = BuildRequest(settings)
                .AddQueryParam("action", "getsettings");

            var result = ProcessRequest(requestBuilder, settings);

            var configuration = new Dictionary<string, string>();

            foreach (var configItem in result.Settings)
            {
                configuration.Add(configItem[0].ToString(), configItem[2].ToString());
            }

            return configuration;
        }

        public UTorrentResponse GetTorrents(string cacheID, UTorrentSettings settings)
        {
            var requestBuilder = BuildRequest(settings)
                .AddQueryParam("list", 1);

            if (cacheID.IsNotNullOrWhiteSpace())
            {
                requestBuilder.AddQueryParam("cid", cacheID);
            }

            var result = ProcessRequest(requestBuilder, settings);

            return result;
        }

        public void AddTorrentFromUrl(string torrentUrl, UTorrentSettings settings)
        {
            var requestBuilder = BuildRequest(settings)
                .AddQueryParam("action", "add-url")
                .AddQueryParam("s", torrentUrl);

            ProcessRequest(requestBuilder, settings);
        }

        public void AddTorrentFromFile(string fileName, byte[] fileContent, UTorrentSettings settings)
        {
            var requestBuilder = BuildRequest(settings)
                .Post()
                .AddQueryParam("action", "add-file")
                .AddQueryParam("path", string.Empty)
                .AddFormUpload("torrent_file", fileName, fileContent, @"application/octet-stream");

            ProcessRequest(requestBuilder, settings);
        }

        public void SetTorrentSeedingConfiguration(string hash, TorrentSeedConfiguration seedConfiguration, UTorrentSettings settings)
        {
            var requestBuilder = BuildRequest(settings)
                .AddQueryParam("action", "setprops")
                .AddQueryParam("hash", hash);
            
            requestBuilder.AddQueryParam("s", "seed_override")
                          .AddQueryParam("v", 1);

            if (seedConfiguration.Ratio != null)
            {
                requestBuilder.AddQueryParam("s", "seed_ratio")
                              .AddQueryParam("v", Convert.ToInt32(seedConfiguration.Ratio.Value * 1000));
            }

            if (seedConfiguration.SeedTime != null)
            {
                requestBuilder.AddQueryParam("s", "seed_time")
                              .AddQueryParam("v", Convert.ToInt32(seedConfiguration.SeedTime.Value.TotalSeconds));
            }

            ProcessRequest(requestBuilder, settings);
        }

        public void RemoveTorrent(string hash, bool removeData, UTorrentSettings settings)
        {
            var requestBuilder = BuildRequest(settings)
                .AddQueryParam("action", removeData ? "removedata" : "remove")
                .AddQueryParam("hash", hash);

            ProcessRequest(requestBuilder, settings);
        }

        public void SetTorrentLabel(string hash, string label, UTorrentSettings settings)
        {
            var requestBuilder = BuildRequest(settings)
                .AddQueryParam("action", "setprops")
                .AddQueryParam("hash", hash);

            requestBuilder.AddQueryParam("s", "label")
                          .AddQueryParam("v", label);

            ProcessRequest(requestBuilder, settings);
        }

        public void MoveTorrentToTopInQueue(string hash, UTorrentSettings settings)
        {
            var requestBuilder = BuildRequest(settings)
                .AddQueryParam("action", "queuetop")
                .AddQueryParam("hash", hash);

            ProcessRequest(requestBuilder, settings);
        }

        public void StartTorrent(string hash, UTorrentSettings settings)
        {
            var requestBuilder = BuildRequest(settings)
                .AddQueryParam("action", "start")
                .AddQueryParam("hash", hash);

            ProcessRequest(requestBuilder, settings);
        }

        public void StopTorrent(string hash, UTorrentSettings settings)
        {
            var requestBuilder = BuildRequest(settings)
                .AddQueryParam("action", "stop")
                .AddQueryParam("hash", hash);

            ProcessRequest(requestBuilder, settings);
        }

        private HttpRequestBuilder BuildRequest(UTorrentSettings settings)
        {
            var requestBuilder = new HttpRequestBuilder(false, settings.Host, settings.Port)
                .Resource("/gui/")
                .KeepAlive()
                .SetHeader("Cache-Control", "no-cache")
                .Accept(HttpAccept.Json);

            requestBuilder.LogResponseContent = true;
            requestBuilder.NetworkCredential = new NetworkCredential(settings.Username, settings.Password);

            return requestBuilder;
        }

        public UTorrentResponse ProcessRequest(HttpRequestBuilder requestBuilder, UTorrentSettings settings)
        {
            AuthenticateClient(requestBuilder, settings);

            var request = requestBuilder.Build();

            HttpResponse response;
            try
            {
                response = _httpClient.Execute(request);
            }
            catch (HttpException ex)
            {
                if (ex.Response.StatusCode == HttpStatusCode.BadRequest || ex.Response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _logger.Debug("Authentication required, logging in.");

                    AuthenticateClient(requestBuilder, settings, true);

                    request = requestBuilder.Build();

                    response = _httpClient.Execute(request);
                }
                else
                {
                    throw new DownloadClientException("Unable to connect to uTorrent, please check your settings", ex);
                }
            }
            catch (WebException ex)
            {
                throw new DownloadClientException("Unable to connect to uTorrent, please check your settings", ex);
            }

            return Json.Deserialize<UTorrentResponse>(response.Content);
        }

        private void AuthenticateClient(HttpRequestBuilder requestBuilder, UTorrentSettings settings, bool reauthenticate = false)
        {
            var authKey = string.Format("{0}:{1}", requestBuilder.BaseUrl, settings.Password);

            var cookies = _authCookieCache.Find(authKey);
            var authToken = _authTokenCache.Find(authKey);

            if (cookies == null || authToken == null || reauthenticate)
            {
                _authCookieCache.Remove(authKey);
                _authTokenCache.Remove(authKey);

                var authLoginRequest = BuildRequest(settings).Resource("/gui/token.html").Build();

                HttpResponse response;
                try
                {
                    response = _httpClient.Execute(authLoginRequest);
                    _logger.Debug("uTorrent authentication succeeded.");

                    var xmlDoc = new System.Xml.XmlDocument();
                    xmlDoc.LoadXml(response.Content);

                    authToken = xmlDoc.FirstChild.FirstChild.InnerText;
                }
                catch (HttpException ex)
                {
                    if (ex.Response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        _logger.Debug("uTorrent authentication failed.");
                        throw new DownloadClientAuthenticationException("Failed to authenticate with uTorrent.");
                    }

                    throw new DownloadClientException("Unable to connect to uTorrent, please check your settings", ex);
                }
                catch (WebException ex)
                {
                    throw new DownloadClientUnavailableException("Unable to connect to uTorrent, please check your settings", ex);
                }

                cookies = response.GetCookies();

                _authCookieCache.Set(authKey, cookies);
                _authTokenCache.Set(authKey, authToken);
            }

            requestBuilder.SetCookies(cookies);
            requestBuilder.AddPrefixQueryParam("token", authToken, true);
        }
    }
}
