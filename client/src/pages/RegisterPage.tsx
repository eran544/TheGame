import React, { useState, FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import Layout from '../components/layout/Layout';
import Input from '../components/ui/Input';
import Button from '../components/ui/Button';
import useAppDispatch from '../hooks/useAppDispatch';
import useAppSelector from '../hooks/useAppSelector';
import { registerAsync, clearAuthError } from '../store/slices/authSlice';
import styles from './RegisterPage.module.css';

const RegisterPage: React.FC = () => {
  const dispatch = useAppDispatch();
  const navigate = useNavigate();
  const { status, error } = useAppSelector((state) => state.auth);

  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [passwordConfirmation, setPasswordConfirmation] = useState('');
  const [localError, setLocalError] = useState<string | null>(null);

  const isLoading = status === 'loading';

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setLocalError(null);
    dispatch(clearAuthError());

    if (password !== passwordConfirmation) {
      setLocalError('Passwords do not match.');
      return;
    }

    const result = await dispatch(
      registerAsync({ username, password, passwordConfirmation })
    );
    if (registerAsync.fulfilled.match(result)) {
      navigate('/');
    }
  };

  const displayError = localError ?? error;

  return (
    <Layout showHeader={false}>
      <div className={styles.page}>
        <div className={styles.card}>
          <h1 className={styles.title}>
            The <span>Game</span>
          </h1>
          <p className={styles.subtitle}>Create your account</p>

          <form className={styles.form} onSubmit={handleSubmit} noValidate>
            {displayError && (
              <div className={styles.errorBanner} role="alert">
                {displayError}
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
              autoComplete="new-password"
              required
              disabled={isLoading}
            />

            <Input
              label="Confirm Password"
              type="password"
              value={passwordConfirmation}
              onChange={(e) => setPasswordConfirmation(e.target.value)}
              autoComplete="new-password"
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
              Create Account
            </Button>
          </form>

          <p className={styles.footer}>
            Already have an account?{' '}
            <Link to="/login">Sign in</Link>
          </p>
        </div>
      </div>
    </Layout>
  );
};

export default RegisterPage;
