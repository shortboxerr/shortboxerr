import { useState, useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { 
  Calendar as CalendarIcon, 
  ChevronLeft, 
  ChevronRight, 
  RefreshCw,
  Filter,
  X,
  List,
  Grid
} from 'lucide-react';
import { api } from '../api/client';
import type { CalendarDay, PullListIssue, IssueStatus } from '../api/client';
import { Link } from 'react-router-dom';

type DisplayMode = 'calendar' | 'agenda';

const DAYS_OF_WEEK = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
const MONTHS = [
  'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December'
];

export function CalendarPage() {
  const [currentDate, setCurrentDate] = useState(new Date());
  const [displayMode, setDisplayMode] = useState<DisplayMode>('calendar');
  const [statusFilter, setStatusFilter] = useState<IssueStatus | 'all'>('all');
  const [selectedDay, setSelectedDay] = useState<CalendarDay | null>(null);

  // Calculate month boundaries for API call
  const { startDate, endDate } = useMemo(() => {
    const year = currentDate.getFullYear();
    const month = currentDate.getMonth();
    
    // Get first day of month and last day of month
    const firstOfMonth = new Date(year, month, 1);
    const lastOfMonth = new Date(year, month + 1, 0);
    
    // Extend to full weeks (Sunday to Saturday)
    const startDay = new Date(firstOfMonth);
    startDay.setDate(startDay.getDate() - startDay.getDay());
    
    const endDay = new Date(lastOfMonth);
    endDay.setDate(endDay.getDate() + (6 - endDay.getDay()));
    
    return {
      startDate: startDay.toISOString().split('T')[0],
      endDate: endDay.toISOString().split('T')[0]
    };
  }, [currentDate]);

  // Fetch calendar data
  const { data: calendar, isLoading, isFetching, refetch } = useQuery({
    queryKey: ['calendar', startDate, endDate],
    queryFn: () => api.getPullListCalendar(startDate, endDate),
    staleTime: 30 * 60 * 1000, // 30 minutes
  });

  // Navigate months
  const goToPreviousMonth = () => {
    setCurrentDate(prev => new Date(prev.getFullYear(), prev.getMonth() - 1, 1));
    setSelectedDay(null);
  };

  const goToNextMonth = () => {
    setCurrentDate(prev => new Date(prev.getFullYear(), prev.getMonth() + 1, 1));
    setSelectedDay(null);
  };

  const goToToday = () => {
    setCurrentDate(new Date());
    setSelectedDay(null);
  };

  // Filter issues by status
  const filterIssues = (issues: PullListIssue[]): PullListIssue[] => {
    if (statusFilter === 'all') return issues;
    return issues.filter(i => i.status === statusFilter);
  };

  // Get status badge
  const getStatusBadge = (status: IssueStatus | null) => {
    if (!status) return null;
    switch (status) {
      case 'Owned':
        return <span className="badge badge-success">Owned</span>;
      case 'Wanted':
        return <span className="badge badge-warning">Wanted</span>;
      case 'Skipped':
        return <span className="badge badge-secondary">Skipped</span>;
      case 'Downloading':
        return <span className="badge badge-info">Downloading</span>;
      case 'Missing':
        return <span className="badge badge-danger">Missing</span>;
      default:
        return <span className="badge">{status}</span>;
    }
  };

  // Get status indicator color class
  const getStatusClass = (status: IssueStatus | null): string => {
    switch (status) {
      case 'Owned': return 'status-owned';
      case 'Wanted': return 'status-wanted';
      case 'Skipped': return 'status-skipped';
      case 'Downloading': return 'status-downloading';
      case 'Missing': return 'status-missing';
      default: return '';
    }
  };

  // Build calendar grid
  const calendarGrid = useMemo(() => {
    if (!calendar?.days) return [];
    
    const dayMap = new Map<string, CalendarDay>();
    for (const day of calendar.days) {
      dayMap.set(day.date.split('T')[0], day);
    }
    
    const weeks: (CalendarDay | null)[][] = [];
    let currentWeek: (CalendarDay | null)[] = [];
    
    const start = new Date(startDate);
    const end = new Date(endDate);
    
    for (let d = new Date(start); d <= end; d.setDate(d.getDate() + 1)) {
      const dateStr = d.toISOString().split('T')[0];
      const calDay = dayMap.get(dateStr) || { 
        date: dateStr, 
        isReleaseDay: false, 
        issues: [] 
      };
      currentWeek.push(calDay);
      
      if (currentWeek.length === 7) {
        weeks.push(currentWeek);
        currentWeek = [];
      }
    }
    
    if (currentWeek.length > 0) {
      weeks.push(currentWeek);
    }
    
    return weeks;
  }, [calendar, startDate, endDate]);

  // Check if a date is today
  const isToday = (dateStr: string): boolean => {
    const today = new Date().toISOString().split('T')[0];
    return dateStr.split('T')[0] === today;
  };

  // Check if a date is in current month
  const isCurrentMonth = (dateStr: string): boolean => {
    const date = new Date(dateStr);
    return date.getMonth() === currentDate.getMonth() && 
           date.getFullYear() === currentDate.getFullYear();
  };

  // Format date for display
  const formatDate = (dateStr: string): string => {
    const date = new Date(dateStr);
    return date.toLocaleDateString('en-US', { 
      weekday: 'long',
      month: 'short', 
      day: 'numeric',
      year: 'numeric',
      timeZone: 'UTC'
    });
  };

  // Get day number from date
  const getDayNumber = (dateStr: string): number => {
    return new Date(dateStr).getUTCDate();
  };

  // Render calendar cell
  const renderCalendarCell = (day: CalendarDay | null) => {
    if (!day) return <td className="calendar-cell empty" />;
    
    const filtered = filterIssues(day.issues);
    const hasIssues = filtered.length > 0;
    const isSelected = selectedDay?.date === day.date;
    
    return (
      <td 
        key={day.date}
        className={`calendar-cell ${!isCurrentMonth(day.date) ? 'other-month' : ''} ${isToday(day.date) ? 'today' : ''} ${hasIssues ? 'has-issues' : ''} ${day.isReleaseDay ? 'release-day' : ''} ${isSelected ? 'selected' : ''}`}
        onClick={() => hasIssues && setSelectedDay(isSelected ? null : day)}
      >
        <div className="calendar-cell-header">
          <span className="calendar-day-number">{getDayNumber(day.date)}</span>
          {day.isReleaseDay && <span className="release-day-badge">Release Day</span>}
        </div>
        {hasIssues && (
          <div className="calendar-cell-content">
            <div className="calendar-issue-count">{filtered.length} issue{filtered.length !== 1 ? 's' : ''}</div>
            <div className="calendar-issue-dots">
              {filtered.slice(0, 5).map((issue, i) => (
                <span 
                  key={issue.issueId || i} 
                  className={`issue-dot ${getStatusClass(issue.status)}`}
                  title={`${issue.seriesTitle} #${issue.issueNumber}`}
                />
              ))}
              {filtered.length > 5 && <span className="issue-dot more">+{filtered.length - 5}</span>}
            </div>
          </div>
        )}
      </td>
    );
  };

  // Render agenda view
  const renderAgendaView = () => {
    if (!calendar?.days) return null;
    
    const daysWithIssues = calendar.days
      .filter(day => filterIssues(day.issues).length > 0)
      .sort((a, b) => a.date.localeCompare(b.date));
    
    if (daysWithIssues.length === 0) {
      return (
        <div className="empty-state">
          <CalendarIcon size={48} />
          <div className="empty-state-title">No Releases This Month</div>
          <div className="empty-state-text">
            No comic releases found for {MONTHS[currentDate.getMonth()]} {currentDate.getFullYear()}.
          </div>
        </div>
      );
    }
    
    return (
      <div className="agenda-view">
        {daysWithIssues.map(day => {
          const filtered = filterIssues(day.issues);
          return (
            <div key={day.date} className={`agenda-day ${day.isReleaseDay ? 'release-day' : ''}`}>
              <div className="agenda-day-header">
                <span className="agenda-day-date">{formatDate(day.date)}</span>
                {day.isReleaseDay && <span className="badge badge-accent">Release Day</span>}
                <span className="agenda-day-count">{filtered.length} issue{filtered.length !== 1 ? 's' : ''}</span>
              </div>
              <div className="agenda-day-issues">
                {filtered.map(issue => (
                  <div key={issue.issueId} className="agenda-issue">
                    {issue.coverImageUrl ? (
                      <img src={issue.coverImageUrl} alt="" className="agenda-issue-cover" />
                    ) : (
                      <div className="agenda-issue-cover-placeholder">
                        <CalendarIcon size={16} />
                      </div>
                    )}
                    <div className="agenda-issue-info">
                      <Link to={`/series/${issue.seriesId}`} className="agenda-issue-series">
                        {issue.seriesTitle}
                      </Link>
                      <div className="agenda-issue-number">
                        #{issue.issueNumberText || issue.issueNumber}
                        {issue.issueTitle && <span className="agenda-issue-title"> - {issue.issueTitle}</span>}
                      </div>
                      {issue.publisher && <div className="agenda-issue-publisher">{issue.publisher}</div>}
                    </div>
                    <div className="agenda-issue-status">
                      {getStatusBadge(issue.status)}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          );
        })}
      </div>
    );
  };

  // Render selected day details
  const renderSelectedDayDetails = () => {
    if (!selectedDay) return null;
    
    const filtered = filterIssues(selectedDay.issues);
    
    return (
      <div className="calendar-day-detail">
        <div className="calendar-day-detail-header">
          <h3>{formatDate(selectedDay.date)}</h3>
          <button className="btn btn-icon btn-sm" onClick={() => setSelectedDay(null)}>
            <X size={16} />
          </button>
        </div>
        <div className="calendar-day-detail-content">
          {filtered.length === 0 ? (
            <div className="empty-state-small">No issues match the filter</div>
          ) : (
            <div className="calendar-issues-list">
              {filtered.map(issue => (
                <div key={issue.issueId} className="calendar-issue-item">
                  {issue.coverImageUrl ? (
                    <img src={issue.coverImageUrl} alt="" className="calendar-issue-thumb" />
                  ) : (
                    <div className="calendar-issue-thumb-placeholder">
                      <CalendarIcon size={16} />
                    </div>
                  )}
                  <div className="calendar-issue-info">
                    <Link to={`/series/${issue.seriesId}`} className="calendar-issue-series">
                      {issue.seriesTitle}
                    </Link>
                    <div className="calendar-issue-number">
                      #{issue.issueNumberText || issue.issueNumber}
                      {issue.issueTitle && <span className="text-muted"> - {issue.issueTitle}</span>}
                    </div>
                    {issue.publisher && <div className="calendar-issue-publisher text-muted">{issue.publisher}</div>}
                  </div>
                  <div className="calendar-issue-status">
                    {getStatusBadge(issue.status)}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    );
  };

  return (
    <>
      <header className="page-header">
        <h1 className="page-title">
          <CalendarIcon className="page-title-icon" size={24} />
          Calendar
        </h1>
      </header>

      <div className="page-content">
        <div className="toolbar">
          {/* Month navigation */}
          <div className="toolbar-group">
            <button className="btn btn-icon" onClick={goToPreviousMonth} title="Previous Month">
              <ChevronLeft size={18} />
            </button>
            <div className="calendar-month-display">
              <span className="calendar-month-name">
                {MONTHS[currentDate.getMonth()]} {currentDate.getFullYear()}
              </span>
            </div>
            <button className="btn btn-icon" onClick={goToNextMonth} title="Next Month">
              <ChevronRight size={18} />
            </button>
            <button className="btn btn-secondary" onClick={goToToday}>
              Today
            </button>
          </div>

          {/* Filters */}
          <div className="toolbar-group">
            <Filter size={16} className="text-muted" />
            <select 
              className="select"
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value as IssueStatus | 'all')}
            >
              <option value="all">All Statuses</option>
              <option value="Wanted">Wanted</option>
              <option value="Owned">Owned</option>
              <option value="Skipped">Skipped</option>
              <option value="Missing">Missing</option>
            </select>
          </div>

          <div className="toolbar-spacer" />

          {/* Display mode toggle */}
          <div className="toolbar-group btn-group">
            <button 
              className={`btn btn-icon ${displayMode === 'calendar' ? 'btn-primary' : 'btn-secondary'}`}
              onClick={() => setDisplayMode('calendar')}
              title="Calendar View"
            >
              <Grid size={18} />
            </button>
            <button 
              className={`btn btn-icon ${displayMode === 'agenda' ? 'btn-primary' : 'btn-secondary'}`}
              onClick={() => setDisplayMode('agenda')}
              title="Agenda View"
            >
              <List size={18} />
            </button>
          </div>

          <div className="toolbar-group">
            <button 
              className="btn btn-icon" 
              onClick={() => refetch()} 
              title="Refresh"
              disabled={isFetching}
            >
              <RefreshCw size={18} className={isFetching ? 'spin' : ''} />
            </button>
          </div>
        </div>

        {/* Content */}
        {isLoading ? (
          <div className="loading"><div className="spinner" /></div>
        ) : displayMode === 'calendar' ? (
          <div className="calendar-container">
            <div className="calendar-grid-wrapper">
              <table className="calendar-grid">
                <thead>
                  <tr>
                    {DAYS_OF_WEEK.map(day => (
                      <th key={day} className="calendar-header-cell">{day}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {calendarGrid.map((week, weekIndex) => (
                    <tr key={weekIndex}>
                      {week.map((day) => renderCalendarCell(day))}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {renderSelectedDayDetails()}
          </div>
        ) : (
          renderAgendaView()
        )}
      </div>
    </>
  );
}
