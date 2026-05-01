import React from 'react';
import { Navigate, Outlet } from 'react-router-dom';
import useAppSelector from '../hooks/useAppSelector';

const AdminRoute: React.FC = () => {
  const { token, user } = useAppSelector((state) => state.auth);

  if (!token || !user?.isAdmin) {
    return <Navigate to="/" replace />;
  }

  return <Outlet />;
};

export default AdminRoute;
