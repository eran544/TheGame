import React from 'react';
import { Link, useNavigate } from 'react-router-dom';
import useAppSelector from '../../hooks/useAppSelector';
import useAppDispatch from '../../hooks/useAppDispatch';
import { logoutAsync } from '../../store/slices/authSlice';
import Button from '../ui/Button';
import styles from './Header.module.css';

const Header: React.FC = () => {
  const dispatch = useAppDispatch();
  const navigate = useNavigate();
  const { user, token, status } = useAppSelector((state) => state.auth);

  const isAuthenticated = Boolean(token && user);

  const handleLogout = async () => {
    await dispatch(logoutAsync());
    navigate('/login');
  };

  return (
    <header className={styles.header}>
      <Link to="/" className={styles.logo}>
        The <span>Game</span>
      </Link>

      {isAuthenticated && user && (
        <div className={styles.right}>
          <span className={styles.username}>
            Playing as <strong>{user.username}</strong>
          </span>
          <Button
            variant="ghost"
            size="sm"
            onClick={handleLogout}
            isLoading={status === 'loading'}
          >
            Logout
          </Button>
        </div>
      )}
    </header>
  );
};

export default Header;
