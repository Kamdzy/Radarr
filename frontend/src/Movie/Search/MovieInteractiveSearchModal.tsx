import React, { useCallback } from 'react';
import { useDispatch } from 'react-redux';
import Modal from 'Components/Modal/Modal';
import { sizes } from 'Helpers/Props';
import { clearMovieBlocklist } from 'Store/Actions/movieBlocklistActions';
import { clearMovieHistory } from 'Store/Actions/movieHistoryActions';
import {
  cancelFetchReleases,
  clearReleases,
} from 'Store/Actions/releaseActions';
import MovieInteractiveSearchModalContent, {
  MovieInteractiveSearchModalContentProps,
} from './MovieInteractiveSearchModalContent';

interface MovieInteractiveSearchModalProps
  extends MovieInteractiveSearchModalContentProps {
  isOpen: boolean;
}

function MovieInteractiveSearchModal({
  isOpen,
  movieId,
  onModalClose,
}: MovieInteractiveSearchModalProps) {
  const dispatch = useDispatch();

  const handleModalClose = useCallback(() => {
    dispatch(cancelFetchReleases());
    dispatch(clearReleases());

    dispatch(clearMovieBlocklist());
    dispatch(clearMovieHistory());

    onModalClose();
  }, [dispatch, onModalClose]);

  return (
    <Modal
      isOpen={isOpen}
      closeOnBackgroundClick={false}
      size={sizes.EXTRA_EXTRA_LARGE}
      onModalClose={handleModalClose}
    >
      <MovieInteractiveSearchModalContent
        movieId={movieId}
        onModalClose={handleModalClose}
      />
    </Modal>
  );
}

export default MovieInteractiveSearchModal;
