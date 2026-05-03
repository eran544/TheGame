import React, { useCallback, useEffect, useState } from 'react';
import Layout from '../components/layout/Layout';
import Button from '../components/ui/Button';
import useAppSelector from '../hooks/useAppSelector';
import adminApi, {
  AdminDashboard,
  AdminGame,
  AdminUser,
  CreateUserRequest,
} from '../api/adminApi';
import styles from './AdminPage.module.css';

// ── Stat card ──────────────────────────────────────────────────────────────

const StatCard: React.FC<{ label: string; value: number }> = ({ label, value }) => (
  <div className={styles.statCard}>
    <div className={styles.statValue}>{value}</div>
    <div className={styles.statLabel}>{label}</div>
  </div>
);

// ── Reset-password modal ───────────────────────────────────────────────────

interface ResetPasswordModalProps {
  user: AdminUser;
  token: string;
  onClose: () => void;
}

const ResetPasswordModal: React.FC<ResetPasswordModalProps> = ({ user, token, onClose }) => {
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const handleSubmit = async () => {
    setError('');
    setLoading(true);
    try {
      await adminApi.resetPassword(token, user.id, password);
      onClose();
    } catch (e: unknown) {
      const msg = (e as { response?: { data?: { error?: string } } })?.response?.data?.error;
      setError(msg ?? 'Failed to reset password');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className={styles.modalOverlay}>
      <div className={styles.modal}>
        <div className={styles.modalTitle}>Reset password for {user.username}</div>
        {error && <div className={styles.error}>{error}</div>}
        <div className={styles.formGroup}>
          <label className={styles.formLabel}>New password</label>
          <input
            type="password"
            className={styles.formInput}
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoFocus
          />
        </div>
        <div className={styles.modalActions}>
          <button className={styles.btnSecondary} onClick={onClose}>Cancel</button>
          <Button size="sm" onClick={handleSubmit} isLoading={loading} disabled={!password}>
            Reset
          </Button>
        </div>
      </div>
    </div>
  );
};

// ── Overview tab ───────────────────────────────────────────────────────────

const OverviewTab: React.FC<{ dashboard: AdminDashboard | null }> = ({ dashboard }) => {
  if (!dashboard) return <div className={styles.empty}>Loading...</div>;
  return (
    <div className={styles.statGrid}>
      <StatCard label="Total Users" value={dashboard.totalUsers} />
      <StatCard label="Active Games" value={dashboard.activeGames} />
      <StatCard label="Completed Games" value={dashboard.totalCompletedGames} />
      <StatCard label="Chat Violations" value={dashboard.totalChatViolations} />
    </div>
  );
};

// ── Users tab ──────────────────────────────────────────────────────────────

interface UsersTabProps {
  token: string;
  users: AdminUser[];
  onRefresh: () => void;
}

const UsersTab: React.FC<UsersTabProps> = ({ token, users, onRefresh }) => {
  const [form, setForm] = useState<CreateUserRequest>({ username: '', password: '', isAdmin: false });
  const [createError, setCreateError] = useState('');
  const [createLoading, setCreateLoading] = useState(false);
  const [resetTarget, setResetTarget] = useState<AdminUser | null>(null);

  const handleCreate = async () => {
    setCreateError('');
    setCreateLoading(true);
    try {
      await adminApi.createUser(token, form);
      setForm({ username: '', password: '', isAdmin: false });
      onRefresh();
    } catch (e: unknown) {
      const msg = (e as { response?: { data?: { error?: string } } })?.response?.data?.error;
      setCreateError(msg ?? 'Failed to create user');
    } finally {
      setCreateLoading(false);
    }
  };

  const handleDelete = async (user: AdminUser) => {
    if (!window.confirm(`Delete user "${user.username}"? This cannot be undone.`)) return;
    try {
      await adminApi.deleteUser(token, user.id);
      onRefresh();
    } catch {
      alert('Failed to delete user');
    }
  };

  return (
    <>
      <div className={styles.createForm}>
        <div className={styles.formGroup}>
          <label className={styles.formLabel}>Username</label>
          <input
            className={styles.formInput}
            value={form.username}
            onChange={(e) => setForm((f) => ({ ...f, username: e.target.value }))}
            placeholder="username"
          />
        </div>
        <div className={styles.formGroup}>
          <label className={styles.formLabel}>Password</label>
          <input
            type="password"
            className={styles.formInput}
            value={form.password}
            onChange={(e) => setForm((f) => ({ ...f, password: e.target.value }))}
            placeholder="password"
          />
        </div>
        <div className={styles.checkboxGroup}>
          <input
            type="checkbox"
            id="isAdmin"
            checked={form.isAdmin}
            onChange={(e) => setForm((f) => ({ ...f, isAdmin: e.target.checked }))}
          />
          <label htmlFor="isAdmin" style={{ fontSize: '0.9rem' }}>Admin</label>
        </div>
        {createError && <div className={styles.error}>{createError}</div>}
        <Button
          size="sm"
          onClick={handleCreate}
          isLoading={createLoading}
          disabled={!form.username || !form.password}
        >
          Create User
        </Button>
      </div>

      <div className={styles.tableWrap}>
        <table className={styles.table}>
          <thead>
            <tr>
              <th>Username</th>
              <th>Role</th>
              <th>Created</th>
              <th>Last Login</th>
              <th>Games</th>
              <th>Perfect</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {users.length === 0 ? (
              <tr><td colSpan={7} className={styles.empty}>No users</td></tr>
            ) : (
              users.map((u) => (
                <tr key={u.id}>
                  <td>{u.username}</td>
                  <td>
                    {u.isAdmin && <span className={styles.adminBadge}>Admin</span>}
                    {!u.isAdmin && 'Player'}
                  </td>
                  <td>{new Date(u.createdAt).toLocaleDateString()}</td>
                  <td>{u.lastLoginAt ? new Date(u.lastLoginAt).toLocaleDateString() : '—'}</td>
                  <td>{u.totalGames}</td>
                  <td>{u.perfectGames}</td>
                  <td>
                    <div className={styles.actions}>
                      <button
                        className={styles.btnSecondary}
                        onClick={() => setResetTarget(u)}
                      >
                        Reset PW
                      </button>
                      {!u.isAdmin && (
                        <button
                          className={styles.btnDanger}
                          onClick={() => handleDelete(u)}
                        >
                          Delete
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {resetTarget && (
        <ResetPasswordModal
          user={resetTarget}
          token={token}
          onClose={() => setResetTarget(null)}
        />
      )}
    </>
  );
};

// ── Games tab ──────────────────────────────────────────────────────────────

interface GamesTabProps {
  token: string;
  games: AdminGame[];
  onRefresh: () => void;
}

const GamesTab: React.FC<GamesTabProps> = ({ token, games, onRefresh }) => {
  const handleForceEnd = async (game: AdminGame) => {
    if (!window.confirm(`Force-end game hosted by "${game.hostUsername}"?`)) return;
    try {
      await adminApi.forceEndGame(token, game.sessionId);
      onRefresh();
    } catch {
      alert('Failed to end game');
    }
  };

  const handleKick = async (sessionId: string, userId: string, username: string) => {
    if (!window.confirm(`Kick player "${username}"?`)) return;
    try {
      await adminApi.kickPlayer(token, sessionId, userId);
      onRefresh();
    } catch {
      alert('Failed to kick player');
    }
  };

  if (games.length === 0) {
    return <div className={styles.empty}>No active games right now.</div>;
  }

  return (
    <div className={styles.tableWrap}>
      <table className={styles.table}>
        <thead>
          <tr>
            <th>Host</th>
            <th>Players</th>
            <th>Started</th>
            <th>Participants</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {games.map((g) => (
            <tr key={g.sessionId}>
              <td>{g.hostUsername}</td>
              <td>{g.playerCount} / {g.maxPlayers}</td>
              <td>{new Date(g.startedAt).toLocaleTimeString()}</td>
              <td>
                <div className={styles.playerList}>
                  {g.players.map((p) => (
                    <span
                      key={p.userId}
                      className={`${styles.playerChip} ${p.isAI ? styles.playerChipAI : ''}`}
                    >
                      {p.username}
                      {!p.isAI && (
                        <button
                          onClick={() => handleKick(g.sessionId, p.userId, p.username)}
                          style={{ marginLeft: '4px', background: 'none', border: 'none', color: '#e55', cursor: 'pointer', fontSize: '0.7rem' }}
                          title="Kick player"
                        >
                          ✕
                        </button>
                      )}
                    </span>
                  ))}
                </div>
              </td>
              <td>
                <button className={styles.btnDanger} onClick={() => handleForceEnd(g)}>
                  Force End
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
};

// ── AdminPage ──────────────────────────────────────────────────────────────

type Tab = 'overview' | 'users' | 'games';

const AdminPage: React.FC = () => {
  const { token } = useAppSelector((state) => state.auth);
  const [tab, setTab] = useState<Tab>('overview');
  const [dashboard, setDashboard] = useState<AdminDashboard | null>(null);
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [games, setGames] = useState<AdminGame[]>([]);

  const loadData = useCallback(async () => {
    if (!token) return;
    try { setDashboard(await adminApi.getDashboard(token)); } catch { /* ignore */ }
    try { setUsers(await adminApi.getUsers(token)); } catch { /* ignore */ }
    try { setGames(await adminApi.getActiveGames(token)); } catch { /* ignore */ }
  }, [token]);

  useEffect(() => { loadData(); }, [loadData]);

  return (
    <Layout>
      <div className={styles.page}>
        <div className={styles.title}>Admin Dashboard</div>

        <div className={styles.tabs}>
          {(['overview', 'users', 'games'] as Tab[]).map((t) => (
            <button
              key={t}
              className={`${styles.tab} ${tab === t ? styles.tabActive : ''}`}
              onClick={() => setTab(t)}
            >
              {t.charAt(0).toUpperCase() + t.slice(1)}
            </button>
          ))}
        </div>

        {tab === 'overview' && <OverviewTab dashboard={dashboard} />}
        {tab === 'users' && (
          <UsersTab token={token!} users={users} onRefresh={loadData} />
        )}
        {tab === 'games' && (
          <GamesTab token={token!} games={games} onRefresh={loadData} />
        )}
      </div>
    </Layout>
  );
};

export default AdminPage;
