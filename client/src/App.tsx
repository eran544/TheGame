import React from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import ProtectedRoute from './routes/ProtectedRoute';
import AdminRoute from './routes/AdminRoute';
import LoginPage from './pages/LoginPage';
import RegisterPage from './pages/RegisterPage';
import MainMenuPage from './pages/MainMenuPage';
import GamePage from './pages/GamePage';
import MultiplayerPage from './pages/MultiplayerPage';
import LobbyPage from './pages/LobbyPage';
import MultiplayerGamePage from './pages/MultiplayerGamePage';
import DashboardPage from './pages/DashboardPage';
import InstructionsPage from './pages/InstructionsPage';

const App: React.FC = () => {
  return (
    <Routes>
      {/* Public routes */}
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />

      {/* Protected routes */}
      <Route element={<ProtectedRoute />}>
        <Route path="/" element={<MainMenuPage />} />
        <Route path="/game/new" element={<GamePage />} />
        <Route path="/multiplayer" element={<MultiplayerPage />} />
        <Route path="/lobby/:sessionId" element={<LobbyPage />} />
        <Route path="/game/:sessionId" element={<MultiplayerGamePage />} />
        <Route path="/dashboard" element={<DashboardPage />} />
        <Route path="/instructions" element={<InstructionsPage />} />
      </Route>

      {/* Admin routes */}
      <Route element={<AdminRoute />}>
        <Route
          path="/admin"
          element={
            <div style={{ padding: '2rem', color: 'var(--color-text)' }}>
              Admin panel coming soon
            </div>
          }
        />
      </Route>

      {/* Catch-all */}
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
};

export default App;
