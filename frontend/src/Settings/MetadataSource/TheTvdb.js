import React from 'react';
import translate from 'Utilities/String/translate';
import InlineMarkdown from '../../Components/Markdown/InlineMarkdown';
import styles from './TheTvdb.css';

function TheTvdb(props) {
  return (
    <div className={styles.container}>
      <img
        className={styles.image}
        src={`${window.Sonarr.urlBase}/Content/Images/thetvdb.png`}
      />

      <div className={styles.info}>
        <div className={styles.title}>
          {translate('TheTvdb')}
        </div>

        <InlineMarkdown data={translate('SeriesAndEpisodeInformationIsProvidedByTheTVDB')} />
      </div>

    </div>
  );
}

export default TheTvdb;
