import React from 'react';
import Header from './Header';
import styles from './Layout.module.css';

interface LayoutProps {
  children: React.ReactNode;
  showHeader?: boolean;
}

const Layout: React.FC<LayoutProps> = ({ children, showHeader = true }) => {
  return (
    <div className={styles.layout}>
      {showHeader && <Header />}
      <main className={styles.main}>{children}</main>
    </div>
  );
};

export default Layout;
