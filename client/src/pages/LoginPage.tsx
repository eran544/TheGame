import React, { useState, FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import Layout from '../components/layout/Layout';
import Input from '../components/ui/Input';
import Button from '../components/ui/Button';
import useAppDispatch from '../hooks/useAppDispatch';
import useAppSelector from '../hooks/useAppSelector';
import { loginAsync, clearAuthError } from '../store/slices/authSlice';
import styles from './LoginPage.module.css';

const LoginPage: React.FC = () => {
  const dispatch = useAppDispatch();
  const navigate = useNavigate();
  const { status, error } = useAppSelector((state) => state.auth);

  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');

  const isLoading = status === 'loading';

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    dispatch(clearAuthError());
    const result = await dispatch(loginAsync({ username, password }));
    if (loginAsync.fulfilled.match(result)) {
      navigate('/');
    }
  };

  return (
    <Layout showHeader={false}>
      <div className={styles.page}>
        <div className={styles.card}>
          <h1 className={styles.title}>
            The <span>Game</span>
          </h1>
          <p className={styles.subtitle}>Sign in to your account</p>

          <form className={styles.form} onSubmit={handleSubmit} noValidate>
            {error && (
              <div className={styles.errorBanner} role="alert">
                {error}
              </div>
            )}

            <Input
              label="Username"
              type="text"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              autoComplete="username"
              autoFocus
              required
              disabled={isLoading}
            />

            <Input
              label="Password"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              autoComplete="current-password"
              required
              disabled={isLoading}
            />

            <Button
              type="submit"
              variant="primary"
              size="lg"
              fullWidth
              isLoading={isLoading}
            >
              Sign In
            </Button>
          </form>

          <p className={styles.footer}>
            Don't have an account?{' '}
            <Link to="/register">Create one</Link>
          </p>
        </div>
      </div>
    </Layout>
  );
};

export default LoginPage;
