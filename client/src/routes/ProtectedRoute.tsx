import React from 'react';
import { Navigate, Outlet, useLocation } from 'react-router-dom';
import useAppSelector from '../hooks/useAppSelector';

const ProtectedRoute: React.FC = () => {
  const { token } = useAppSelector((state) => state.auth);
  const location = useLocation();

  if (!token) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  return <Outlet />;
};

export default ProtectedRoute;
