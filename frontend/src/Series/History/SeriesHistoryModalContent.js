import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Alert from 'Components/Alert';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import { icons, kinds } from 'Helpers/Props';
import SeasonNumber from 'Season/SeasonNumber';
import translate from 'Utilities/String/translate';
import SeriesHistoryRowConnector from './SeriesHistoryRowConnector';

const columns = [
  {
    name: 'eventType',
    isVisible: true
  },
  {
    name: 'episode',
    label: () => translate('Episode'),
    isVisible: true
  },
  {
    name: 'sourceTitle',
    label: () => translate('SourceTitle'),
    isVisible: true
  },
  {
    name: 'languages',
    label: () => translate('Languages'),
    isVisible: true
  },
  {
    name: 'quality',
    label: () => translate('Quality'),
    isVisible: true
  },
  {
    name: 'date',
    label: () => translate('Date'),
    isVisible: true
  },
  {
    name: 'details',
    label: () => translate('Details'),
    isVisible: true
  },
  {
    name: 'customFormatScore',
    label: React.createElement(Icon, {
      name: icons.SCORE,
      title: () => translate('CustomFormatScore')
    }),
    isSortable: true,
    isVisible: true
  },
  {
    name: 'actions',
    label: () => translate('Actions'),
    isVisible: true
  }
];

class SeriesHistoryModalContent extends Component {

  //
  // Render

  render() {
    const {
      seasonNumber,
      isFetching,
      isPopulated,
      error,
      items,
      onMarkAsFailedPress,
      onModalClose
    } = this.props;

    const fullSeries = seasonNumber == null;
    const hasItems = !!items.length;

    return (
      <ModalContent onModalClose={onModalClose}>
        <ModalHeader>
          History {seasonNumber != null && <SeasonNumber seasonNumber={seasonNumber} />}
        </ModalHeader>

        <ModalBody>
          {
            isFetching &&
              <LoadingIndicator />
          }

          {
            !isFetching && !!error &&
              <Alert kind={kinds.DANGER}>Unable to load history.</Alert>
          }

          {
            isPopulated && !hasItems && !error &&
              <div>No history.</div>
          }

          {
            isPopulated && hasItems && !error &&
              <Table columns={columns}>
                <TableBody>
                  {
                    items.map((item) => {
                      return (
                        <SeriesHistoryRowConnector
                          key={item.id}
                          fullSeries={fullSeries}
                          {...item}
                          onMarkAsFailedPress={onMarkAsFailedPress}
                        />
                      );
                    })
                  }
                </TableBody>
              </Table>
          }
        </ModalBody>

        <ModalFooter>
          <Button onPress={onModalClose}>
            Close
          </Button>
        </ModalFooter>
      </ModalContent>
    );
  }
}

SeriesHistoryModalContent.propTypes = {
  seasonNumber: PropTypes.number,
  isFetching: PropTypes.bool.isRequired,
  isPopulated: PropTypes.bool.isRequired,
  error: PropTypes.object,
  items: PropTypes.arrayOf(PropTypes.object).isRequired,
  onMarkAsFailedPress: PropTypes.func.isRequired,
  onModalClose: PropTypes.func.isRequired
};

export default SeriesHistoryModalContent;
