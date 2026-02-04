import { useState, useEffect, useRef } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
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

function getLevelColor(level?: string): string {
  switch (level?.toUpperCase()) {
    case 'VRB':
      return 'text-gray-400';
    case 'DBG':
      return 'text-blue-400';
    case 'INF':
      return 'text-green-400';
    case 'WRN':
      return 'text-yellow-400';
    case 'ERR':
      return 'text-red-400';
    case 'FTL':
      return 'text-red-600 font-bold';
    default:
      return 'text-gray-300';
  }
}

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

  return (
    <div className="log-line flex gap-2 py-0.5 px-2 hover:bg-gray-800/50 font-mono text-xs border-b border-gray-800/30">
      <span className="flex-shrink-0">{getLevelIcon(line.level)}</span>
      <span className="flex-shrink-0 text-gray-500 w-[180px]">
        {line.timestamp ? new Date(line.timestamp).toLocaleString() : ''}
      </span>
      <span className={`flex-shrink-0 w-[40px] ${getLevelColor(line.level)}`}>
        {line.level || ''}
      </span>
      <span className="flex-shrink-0 text-purple-400 w-[200px] truncate" title={line.category}>
        {line.category || ''}
      </span>
      <span className="flex-grow text-gray-200 break-all">
        {highlightSearch(line.message || line.raw)}
      </span>
    </div>
  );
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
  });

  // Fetch log content
  const { data: logContent, isLoading: contentLoading, refetch: refetchLogs } = useQuery({
    queryKey: ['logContent', selectedFile, lineCount, levelFilter, searchTerm],
    queryFn: () => 
      selectedFile === 'recent'
        ? api.getRecentLogs(lineCount, levelFilter || undefined, searchTerm || undefined)
        : api.getLogContent(selectedFile, lineCount, levelFilter || undefined, searchTerm || undefined),
    refetchInterval: selectedFile === 'recent' ? 5000 : false, // Auto-refresh recent logs
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

  // Auto-scroll to bottom when new logs come in
  useEffect(() => {
    if (autoScroll && logContainerRef.current) {
      logContainerRef.current.scrollTop = logContainerRef.current.scrollHeight;
    }
  }, [logContent?.lines, autoScroll]);

  const handleDownload = (file: LogFile) => {
    // Create a download link
    const url = `/api/v1/system/logs/${encodeURIComponent(file.fileName)}?lines=999999`;
    window.open(url, '_blank');
  };

  return (
    <div className="logs-page flex flex-col h-full">
      {/* Header */}
      <div className="flex items-center justify-between p-4 border-b border-gray-700">
        <h1 className="text-2xl font-bold text-gray-100">System Logs</h1>
        <div className="flex items-center gap-2">
          <button
            className="btn btn-icon"
            onClick={() => refetchLogs()}
            title="Refresh"
          >
            <RefreshCw size={18} className={contentLoading ? 'animate-spin' : ''} />
          </button>
        </div>
      </div>

      {/* Toolbar */}
      <div className="flex flex-wrap items-center gap-3 p-4 bg-gray-800/50 border-b border-gray-700">
        {/* File selector */}
        <select
          className="input w-48"
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

        {/* Level filter */}
        <select
          className="input w-36"
          value={levelFilter}
          onChange={(e) => setLevelFilter(e.target.value)}
        >
          {LOG_LEVELS.map((level) => (
            <option key={level.value} value={level.value}>
              {level.label}
            </option>
          ))}
        </select>

        {/* Search */}
        <div className="relative flex-grow max-w-xs">
          <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" />
          <input
            type="text"
            className="input pl-9 w-full"
            placeholder="Search logs..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
          />
        </div>

        {/* Line count */}
        <select
          className="input w-28"
          value={lineCount}
          onChange={(e) => setLineCount(Number(e.target.value))}
        >
          <option value={100}>100 lines</option>
          <option value={250}>250 lines</option>
          <option value={500}>500 lines</option>
          <option value={1000}>1000 lines</option>
          <option value={5000}>5000 lines</option>
        </select>

        {/* Auto-scroll toggle */}
        <label className="flex items-center gap-2 text-sm text-gray-300 cursor-pointer">
          <input
            type="checkbox"
            checked={autoScroll}
            onChange={(e) => setAutoScroll(e.target.checked)}
            className="rounded border-gray-600 bg-gray-700 text-blue-500"
          />
          Auto-scroll
        </label>
      </div>

      {/* Stats bar */}
      {logContent && (
        <div className="flex items-center gap-4 px-4 py-2 bg-gray-900/50 text-xs text-gray-400 border-b border-gray-700">
          <span>Total: {logContent.totalLines.toLocaleString()} lines</span>
          {(levelFilter || searchTerm) && (
            <span>Filtered: {logContent.filteredLines.toLocaleString()} lines</span>
          )}
          <span>Showing: {logContent.returnedLines.toLocaleString()} lines</span>
        </div>
      )}

      {/* Log content */}
      <div
        ref={logContainerRef}
        className="flex-grow overflow-auto bg-gray-900"
        style={{ fontFamily: 'ui-monospace, SFMono-Regular, "SF Mono", Menlo, Consolas, monospace' }}
      >
        {contentLoading && !logContent ? (
          <div className="flex items-center justify-center h-32 text-gray-400">
            <RefreshCw size={24} className="animate-spin mr-2" />
            Loading logs...
          </div>
        ) : logContent?.lines.length === 0 ? (
          <div className="flex flex-col items-center justify-center h-32 text-gray-400">
            <FileText size={32} className="mb-2" />
            <p>No log entries found</p>
            {(levelFilter || searchTerm) && (
              <p className="text-sm mt-1">Try adjusting your filters</p>
            )}
          </div>
        ) : (
          <div className="py-1">
            {logContent?.lines.map((line, i) => (
              <LogLineComponent key={i} line={line} searchTerm={searchTerm} />
            ))}
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
