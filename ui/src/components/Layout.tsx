import { useState, useEffect } from 'react';
import { Outlet, NavLink, Link, useLocation } from 'react-router-dom';
import {
  LayoutDashboard,
  BookOpen,
  Library,
  Search,
  Calendar,
  CalendarDays,
  Download,
  History,
  FolderInput,
  Settings,
  Zap,
  ScrollText,
  Menu,
  X,
} from 'lucide-react';
import clsx from 'clsx';

const navItems = [
  { to: '/', icon: LayoutDashboard, label: 'Dashboard' },
  { to: '/series', icon: BookOpen, label: 'Series' },
  { to: '/collections', icon: Library, label: 'Collections' },
  { to: '/wanted', icon: Search, label: 'Wanted' },
  { to: '/pulllist', icon: Calendar, label: 'Pull List' },
  { to: '/calendar', icon: CalendarDays, label: 'Calendar' },
  { to: '/activity', icon: Download, label: 'Queue' },
  { to: '/history', icon: History, label: 'History' },
  { to: '/import', icon: FolderInput, label: 'Manual Import' },
  { to: '/settings', icon: Settings, label: 'Settings' },
  { to: '/logs', icon: ScrollText, label: 'Logs' },
];

export function Layout() {
  const [sidebarOpen, setSidebarOpen] = useState(false); // For mobile slide-out only
  const location = useLocation();

  // Close mobile sidebar when route changes
  useEffect(() => {
    setSidebarOpen(false);
  }, [location.pathname]);

  // Close sidebar when clicking outside (mobile)
  const handleOverlayClick = () => {
    setSidebarOpen(false);
  };

  return (
    <div className={clsx('app-layout', sidebarOpen && 'sidebar-open')}>
      {/* Mobile header with hamburger menu */}
      <header className="mobile-header">
        <Link to="/" className="mobile-header-logo">
          <Zap size={22} />
        </Link>
        <button 
          className="btn btn-icon mobile-menu-btn" 
          onClick={() => setSidebarOpen(!sidebarOpen)}
          aria-label={sidebarOpen ? 'Close menu' : 'Open menu'}
        >
          <Menu size={24} />
        </button>
      </header>

      {/* Overlay for mobile when sidebar is open */}
      {sidebarOpen && (
        <div className="sidebar-overlay" onClick={handleOverlayClick} />
      )}

      <aside className={clsx('sidebar', sidebarOpen && 'open')}>
        <div className="sidebar-header">
          <Link to="/" className="sidebar-brand">
            <div className="sidebar-logo">
              <Zap size={18} />
            </div>
            <span className="sidebar-title">Shortboxerr</span>
          </Link>
          {/* Mobile close button only */}
          <button 
            className="btn btn-icon sidebar-close-btn" 
            onClick={() => setSidebarOpen(false)}
            aria-label="Close menu"
          >
            <X size={20} />
          </button>
        </div>
        
        <nav className="sidebar-nav">
          <div className="nav-section">
            <div className="nav-section-title">Main</div>
            {navItems.slice(0, 6).map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                className={({ isActive }) =>
                  clsx('nav-item', isActive && 'active')
                }
                end={item.to === '/'}
              >
                <item.icon />
                <span>{item.label}</span>
              </NavLink>
            ))}
          </div>
          
          <div className="nav-section">
            <div className="nav-section-title">Activity</div>
            {navItems.slice(6, 9).map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                className={({ isActive }) =>
                  clsx('nav-item', isActive && 'active')
                }
              >
                <item.icon />
                <span>{item.label}</span>
              </NavLink>
            ))}
          </div>
          
          <div className="nav-section">
            <div className="nav-section-title">System</div>
            {navItems.slice(9).map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                className={({ isActive }) =>
                  clsx('nav-item', isActive && 'active')
                }
              >
                <item.icon />
                <span>{item.label}</span>
              </NavLink>
            ))}
          </div>
        </nav>
        
        <div className="sidebar-footer">
          <span className="version-info" title={`Build: ${__COMMIT_HASH__} (${__BRANCH__})`}>
            v{__APP_VERSION__}
          </span>
        </div>
      </aside>
      
      <main className="main-content">
        <Outlet />
      </main>
    </div>
  );
}

