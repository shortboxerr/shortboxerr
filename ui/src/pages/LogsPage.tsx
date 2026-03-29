import { useState, useEffect, useRef, useCallback } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useVirtualizer } from '@tanstack/react-virtual';
import { api } from '../api/client';
import type { LogFile, LogLine } from '../api/client';
import { RefreshCw, Search, Trash2, Download, FileText, AlertCircle, Info, AlertTriangle, Bug, XCircle } from 'lucide-react';

const LOG_LEVELS = [
  { value: '', label: 'All Levels' },
  { value: 'VRB', label: 'Verbose' },
  { value: 'DBG', label: 'Debug' },
  { value: 'INF', label: 'Information' },
  { value: 'WRN', label: 'Warning' },
  { value: 'ERR', label: 'Error' },
  { value: 'FTL', label: 'Fatal' },
];


function getLevelIcon(level?: string) {
  switch (level?.toUpperCase()) {
    case 'VRB':
      return <Bug size={14} className="text-gray-400" />;
    case 'DBG':
      return <Bug size={14} className="text-blue-400" />;
    case 'INF':
      return <Info size={14} className="text-green-400" />;
    case 'WRN':
      return <AlertTriangle size={14} className="text-yellow-400" />;
    case 'ERR':
      return <AlertCircle size={14} className="text-red-400" />;
    case 'FTL':
      return <XCircle size={14} className="text-red-600" />;
    default:
      return <FileText size={14} className="text-gray-400" />;
  }
}

function LogLineComponent({ line, searchTerm }: { line: LogLine; searchTerm: string }) {
  const highlightSearch = (text: string) => {
    if (!searchTerm || !text) return text;
    const regex = new RegExp(`(${searchTerm.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')})`, 'gi');
    const parts = text.split(regex);
    return parts.map((part, i) => 
      regex.test(part) ? <mark key={i} className="bg-yellow-500/40 text-yellow-100">{part}</mark> : part
    );
  };

  // Format timestamp - shorter for mobile
  const formatTimestamp = (ts?: string) => {
    if (!ts) return '';
    const date = new Date(ts);
    return date.toLocaleString('en-US', {
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      hour12: false
    });
  };

  const formatTimestampFull = (ts?: string) => {
    if (!ts) return '';
    const date = new Date(ts);
    return date.toLocaleString('en-US', {
      month: '2-digit',
      day: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      hour12: false
    });
  };

  return (
    <div className="log-line">
      {/* Desktop layout */}
      <div className="log-line-desktop">
        <span className="log-icon">{getLevelIcon(line.level)}</span>
        <span className="log-timestamp">{formatTimestampFull(line.timestamp)}</span>
        <span className="log-level" style={{
          backgroundColor: getLevelBgColor(line.level),
          color: getLevelTextColor(line.level),
        }}>
          {line.level || '-'}
        </span>
        <span className="log-category" title={line.category}>{line.category || ''}</span>
        <span className="log-message">{highlightSearch(line.message || line.raw)}</span>
      </div>
      {/* Mobile layout */}
      <div className="log-line-mobile">
        <div className="log-mobile-header">
          <span className="log-level" style={{
            backgroundColor: getLevelBgColor(line.level),
            color: getLevelTextColor(line.level),
          }}>
            {line.level || '-'}
          </span>
          <span className="log-timestamp">{formatTimestamp(line.timestamp)}</span>
          {line.category && <span className="log-category">{line.category}</span>}
        </div>
        <div className="log-message">{highlightSearch(line.message || line.raw)}</div>
      </div>
    </div>
  );
}

function getLevelBgColor(level?: string): string {
  switch (level?.toUpperCase()) {
    case 'VRB': return 'rgba(156, 163, 175, 0.2)';
    case 'DBG': return 'rgba(96, 165, 250, 0.2)';
    case 'INF': return 'rgba(74, 222, 128, 0.2)';
    case 'WRN': return 'rgba(250, 204, 21, 0.2)';
    case 'ERR': return 'rgba(248, 113, 113, 0.2)';
    case 'FTL': return 'rgba(239, 68, 68, 0.4)';
    default: return 'rgba(107, 114, 128, 0.2)';
  }
}

function getLevelTextColor(level?: string): string {
  switch (level?.toUpperCase()) {
    case 'VRB': return '#9ca3af';
    case 'DBG': return '#60a5fa';
    case 'INF': return '#4ade80';
    case 'WRN': return '#facc15';
    case 'ERR': return '#f87171';
    case 'FTL': return '#ef4444';
    default: return '#9ca3af';
  }
}

export default function LogsPage() {
  const queryClient = useQueryClient();
  const logContainerRef = useRef<HTMLDivElement>(null);
  
  const [selectedFile, setSelectedFile] = useState<string>('recent');
  const [levelFilter, setLevelFilter] = useState<string>('');
  const [searchTerm, setSearchTerm] = useState<string>('');
  const [lineCount, setLineCount] = useState<number>(500);
  const [autoScroll, setAutoScroll] = useState<boolean>(true);

  // Fetch log files list
  const { data: logFiles = [] } = useQuery({
    queryKey: ['logFiles'],
    queryFn: api.getLogFiles,
    refetchInterval: 30000, // Refresh every 30s
    refetchIntervalInBackground: false, // Pause when tab not visible
  });

  // Fetch log content
  const { data: logContent, isLoading: contentLoading, refetch: refetchLogs } = useQuery({
    queryKey: ['logContent', selectedFile, lineCount, levelFilter, searchTerm],
    queryFn: () => 
      selectedFile === 'recent'
        ? api.getRecentLogs(lineCount, levelFilter || undefined, searchTerm || undefined)
        : api.getLogContent(selectedFile, lineCount, levelFilter || undefined, searchTerm || undefined),
    refetchInterval: selectedFile === 'recent' ? 5000 : false, // Auto-refresh recent logs
    refetchIntervalInBackground: false, // Pause when tab not visible
  });

  // Delete mutation
  const deleteMutation = useMutation({
    mutationFn: api.deleteLogFile,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['logFiles'] });
      if (selectedFile !== 'recent') {
        setSelectedFile('recent');
      }
    },
  });

  // Virtualizer for efficient rendering of large log files
  const lines = logContent?.lines ?? [];
  // TanStack Virtual: useVirtualizer is intentionally used; rule is informational for React Compiler.
  // eslint-disable-next-line react-hooks/incompatible-library -- virtualized log lines
  const rowVirtualizer = useVirtualizer({
    count: lines.length,
    getScrollElement: () => logContainerRef.current,
    estimateSize: useCallback(() => 32, []), // Estimated row height
    overscan: 10, // Render extra rows for smoother scrolling
  });

  // Auto-scroll to bottom when new logs come in
  useEffect(() => {
    if (autoScroll && lines.length > 0) {
      rowVirtualizer.scrollToIndex(lines.length - 1, { align: 'end' });
    }
  }, [lines.length, autoScroll, rowVirtualizer]);

  const handleDownload = (file: LogFile) => {
    // Create a download link
    const url = `/api/v1/system/logs/${encodeURIComponent(file.fileName)}?lines=999999`;
    window.open(url, '_blank');
  };

  return (
    <div className="logs-page">
      {/* Compact header with integrated controls */}
      <div className="logs-toolbar">
        {/* Top row: Title, source selector, and actions */}
        <div className="logs-toolbar-primary">
          <h1 className="logs-title">System Logs</h1>
          <select
            className="input logs-source-select"
            value={selectedFile}
            onChange={(e) => setSelectedFile(e.target.value)}
          >
            <option value="recent">Recent Logs (Live)</option>
            {logFiles.map((file) => (
              <option key={file.fileName} value={file.fileName}>
                {file.fileName} ({file.sizeFormatted})
              </option>
            ))}
          </select>
          <button
            className="btn btn-icon"
            onClick={() => refetchLogs()}
            title="Refresh"
          >
            <RefreshCw size={18} className={contentLoading ? 'animate-spin' : ''} />
          </button>
        </div>

        {/* Bottom row: Filters in a compact grid */}
        <div className="logs-toolbar-filters">
          <div className="logs-filter-group">
            <select
              className="input"
              value={levelFilter}
              onChange={(e) => setLevelFilter(e.target.value)}
            >
              {LOG_LEVELS.map((level) => (
                <option key={level.value} value={level.value}>
                  {level.label}
                </option>
              ))}
            </select>
            <select
              className="input"
              value={lineCount}
              onChange={(e) => setLineCount(Number(e.target.value))}
            >
              <option value={100}>100</option>
              <option value={250}>250</option>
              <option value={500}>500</option>
              <option value={1000}>1k</option>
              <option value={5000}>5k</option>
            </select>
            <label className="logs-autoscroll">
              <input
                type="checkbox"
                checked={autoScroll}
                onChange={(e) => setAutoScroll(e.target.checked)}
              />
              <span>Auto</span>
            </label>
          </div>
          <div className="logs-search-wrapper">
            <Search size={14} className="logs-search-icon" />
            <input
              type="text"
              className="input logs-search-input"
              placeholder="Search..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
            />
          </div>
          {logContent && (
            <div className="logs-stats">
              <span>{logContent.returnedLines.toLocaleString()}</span>
              <span className="logs-stats-separator">/</span>
              <span className="logs-stats-total">{logContent.totalLines.toLocaleString()}</span>
            </div>
          )}
        </div>
      </div>

      {/* Log content */}
      <div
        ref={logContainerRef}
        className="logs-scroll-container"
      >
        {contentLoading && !logContent ? (
          <div className="flex items-center justify-center h-32 text-gray-400">
            <RefreshCw size={24} className="animate-spin mr-2" />
            Loading logs...
          </div>
        ) : lines.length === 0 ? (
          <div className="flex flex-col items-center justify-center h-32 text-gray-400">
            <FileText size={32} className="mb-2" />
            <p>No log entries found</p>
            {(levelFilter || searchTerm) && (
              <p className="text-sm mt-1">Try adjusting your filters</p>
            )}
          </div>
        ) : (
          <div className="log-content">
            {/* Header row - desktop only */}
            <div className="log-header-row">
              <span></span>
              <span>Timestamp</span>
              <span>Level</span>
              <span>Source</span>
              <span>Message</span>
            </div>
            {/* Virtualized log lines - only renders visible rows */}
            <div 
              className="log-lines"
              style={{ height: `${rowVirtualizer.getTotalSize()}px`, position: 'relative' }}
            >
              {rowVirtualizer.getVirtualItems().map((virtualRow) => (
                <div
                  key={virtualRow.key}
                  style={{
                    position: 'absolute',
                    top: 0,
                    left: 0,
                    width: '100%',
                    height: `${virtualRow.size}px`,
                    transform: `translateY(${virtualRow.start}px)`,
                  }}
                >
                  <LogLineComponent line={lines[virtualRow.index]} searchTerm={searchTerm} />
                </div>
              ))}
            </div>
            {/* Bottom spacer for scroll padding */}
            <div className="logs-bottom-padding" />
          </div>
        )}
      </div>

      {/* Log files list */}
      {logFiles.length > 0 && (
        <div className="border-t border-gray-700 bg-gray-800/50">
          <div className="p-3">
            <h3 className="text-sm font-medium text-gray-300 mb-2">Log Files</h3>
            <div className="flex flex-wrap gap-2">
              {logFiles.map((file) => (
                <div
                  key={file.fileName}
                  className={`flex items-center gap-2 px-3 py-1.5 rounded text-xs border ${
                    selectedFile === file.fileName
                      ? 'bg-blue-600/20 border-blue-500 text-blue-300'
                      : 'bg-gray-700/50 border-gray-600 text-gray-300 hover:bg-gray-700'
                  }`}
                >
                  <FileText size={14} />
                  <button
                    className="hover:underline"
                    onClick={() => setSelectedFile(file.fileName)}
                  >
                    {file.fileName}
                  </button>
                  <span className="text-gray-500">({file.sizeFormatted})</span>
                  <button
                    className="text-gray-400 hover:text-blue-400"
                    onClick={() => handleDownload(file)}
                    title="Download"
                  >
                    <Download size={12} />
                  </button>
                  <button
                    className="text-gray-400 hover:text-red-400"
                    onClick={() => {
                      if (confirm(`Delete ${file.fileName}?`)) {
                        deleteMutation.mutate(file.fileName);
                      }
                    }}
                    title="Delete"
                  >
                    <Trash2 size={12} />
                  </button>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
