import React from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import ProtectedRoute from './routes/ProtectedRoute';
import AdminRoute from './routes/AdminRoute';
import LoginPage from './pages/LoginPage';
import RegisterPage from './pages/RegisterPage';
import GameSelectionPage from './pages/GameSelectionPage';
import Flip7MenuPage from './pages/Flip7MenuPage';
import Flip7SetupPage from './pages/Flip7SetupPage';
import Flip7SoloPage from './pages/Flip7SoloPage';
import Flip7GamePage from './pages/Flip7GamePage';
import MainMenuPage from './pages/MainMenuPage';
import GamePage from './pages/GamePage';
import MultiplayerPage from './pages/MultiplayerPage';
import LobbyPage from './pages/LobbyPage';
import MultiplayerGamePage from './pages/MultiplayerGamePage';
import DashboardPage from './pages/DashboardPage';
import InstructionsPage from './pages/InstructionsPage';
import AdminPage from './pages/AdminPage';

const App: React.FC = () => {
  return (
    <Routes>
      {/* Public routes */}
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />

      {/* Protected routes */}
      <Route element={<ProtectedRoute />}>
        {/* Game selection landing */}
        <Route path="/" element={<GameSelectionPage />} />

        {/* Flip 7 */}
        <Route path="/flip7" element={<Flip7MenuPage />} />
        <Route path="/flip7/setup/:mode" element={<Flip7SetupPage />} />
        <Route path="/flip7/solo/:gameId" element={<Flip7SoloPage />} />
        <Route path="/flip7/game/:gameId" element={<Flip7GamePage />} />

        {/* The Game */}
        <Route path="/the-game" element={<MainMenuPage />} />
        <Route path="/the-game/game/new" element={<GamePage />} />
        <Route path="/the-game/multiplayer" element={<MultiplayerPage />} />
        <Route path="/the-game/lobby/:sessionId" element={<LobbyPage />} />
        <Route path="/the-game/game/:sessionId" element={<MultiplayerGamePage />} />
        <Route path="/the-game/dashboard" element={<DashboardPage />} />
        <Route path="/the-game/instructions" element={<InstructionsPage />} />
      </Route>

      {/* Admin routes */}
      <Route element={<AdminRoute />}>
        <Route path="/admin" element={<AdminPage />} />
      </Route>

      {/* Catch-all */}
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
};

export default App;
