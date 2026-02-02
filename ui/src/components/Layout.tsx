import { Outlet, NavLink } from 'react-router-dom';
import {
  LayoutDashboard,
  BookOpen,
  Library,
  Search,
  Activity,
  History,
  FolderInput,
  Settings,
  Zap,
} from 'lucide-react';
import clsx from 'clsx';

const navItems = [
  { to: '/', icon: LayoutDashboard, label: 'Dashboard' },
  { to: '/series', icon: BookOpen, label: 'Series' },
  { to: '/collections', icon: Library, label: 'Collections' },
  { to: '/wanted', icon: Search, label: 'Wanted' },
  { to: '/activity', icon: Activity, label: 'Activity' },
  { to: '/history', icon: History, label: 'History' },
  { to: '/import', icon: FolderInput, label: 'Manual Import' },
  { to: '/settings', icon: Settings, label: 'Settings' },
];

export function Layout() {
  return (
    <div className="app-layout">
      <aside className="sidebar">
        <div className="sidebar-header">
          <div className="sidebar-logo">
            <Zap size={18} />
          </div>
          <span className="sidebar-title">Shortboxerr</span>
        </div>
        
        <nav className="sidebar-nav">
          <div className="nav-section">
            <div className="nav-section-title">Main</div>
            {navItems.slice(0, 4).map((item) => (
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
            {navItems.slice(4, 7).map((item) => (
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
            {navItems.slice(7).map((item) => (
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
      </aside>
      
      <main className="main-content">
        <Outlet />
      </main>
    </div>
  );
}

