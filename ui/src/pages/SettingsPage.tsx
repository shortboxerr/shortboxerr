import { useState, useRef, useEffect } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useSearchParams } from 'react-router-dom';
import { 
  Settings, Server, Download, Shield, 
  FolderOpen, Plug, Save, Plus, Edit, Trash2, 
  CheckCircle, XCircle, AlertCircle, Play, GripVertical,
  Copy, RefreshCw, X, Database, ExternalLink, Eye, EyeOff, Calendar, FileText, HardDrive, Bell,
  Globe, Activity, Loader2, Search, RotateCcw
} from 'lucide-react';
import { api } from '../api/client';
import type { 
  Provider, CreateProviderRequest, ProviderTestResult, ComicVineTestResult,
  NzbIndexer, NzbIndexerRequest, NzbTestResult, NzbIndexerPreset,
  WebhookProviderSettings, WebhookProviderRequest, NotificationEventType,
  EmailProviderSettings, EmailProviderRequest,
  SearchSettings, PreferredQuality
} from '../api/client';
import { useTheme } from '../App';

type SettingsTab = 'general' | 'indexers' | 'download' | 'notifications' | 'import' | 'ui' | 'security' | 'comicvine' | 'pulllist' | 'search' | 'annuals';

const tabs: { id: SettingsTab; icon: React.ElementType; label: string }[] = [
  { id: 'general', icon: Settings, label: 'General' },
  { id: 'comicvine', icon: Database, label: 'ComicVine' },
  { id: 'pulllist', icon: Calendar, label: 'Pull List' },
  { id: 'annuals', icon: FileText, label: 'Annual Handling' },
  { id: 'search', icon: Search, label: 'Search' },
  { id: 'indexers', icon: Plug, label: 'Indexers' },
  { id: 'download', icon: Download, label: 'Download Clients' },
  { id: 'notifications', icon: Bell, label: 'Notifications' },
  { id: 'import', icon: FolderOpen, label: 'Import' },
  { id: 'ui', icon: Server, label: 'UI' },
  { id: 'security', icon: Shield, label: 'Security' },
];

export function SettingsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const tabFromUrl = searchParams.get('tab') as SettingsTab | null;
  const [activeTab, setActiveTab] = useState<SettingsTab>(
    tabFromUrl && tabs.some(t => t.id === tabFromUrl) ? tabFromUrl : 'general'
  );
  
  // Update URL when tab changes
  const handleTabChange = (tab: SettingsTab) => {
    setActiveTab(tab);
    setSearchParams({ tab });
  };
  
  // Sync with URL on mount/URL change
  useEffect(() => {
    if (tabFromUrl && tabs.some(t => t.id === tabFromUrl)) {
      setActiveTab(tabFromUrl);
    }
  }, [tabFromUrl]);

  return (
    <>
      <header className="page-header">
        <h1 className="page-title">Settings</h1>
        <button className="btn btn-primary">
          <Save size={16} />
          Save Changes
        </button>
      </header>
      
      <div className="page-content">
        <div style={{ display: 'flex', gap: '24px' }}>
          <div style={{ 
            width: '200px', 
            flexShrink: 0,
            background: 'var(--bg-secondary)',
            borderRadius: 'var(--radius-lg)',
            border: '1px solid var(--border-color)',
            padding: '8px',
          }}>
            {tabs.map((tab) => (
              <button
                key={tab.id}
                className={`nav-item ${activeTab === tab.id ? 'active' : ''}`}
                onClick={() => handleTabChange(tab.id)}
                style={{ 
                  width: '100%', 
                  textAlign: 'left',
                  background: activeTab === tab.id ? 'var(--bg-active)' : 'transparent',
                  border: 'none',
                  cursor: 'pointer',
                  borderRadius: 'var(--radius-md)',
                }}
              >
                <tab.icon size={18} />
                <span>{tab.label}</span>
              </button>
            ))}
          </div>
          
          <div style={{ flex: 1 }}>
            {activeTab === 'general' && <GeneralSettings />}
            {activeTab === 'comicvine' && <ComicVineSettingsTab />}
            {activeTab === 'pulllist' && <PullListSettingsTab />}
            {activeTab === 'annuals' && <AnnualHandlingSettingsTab />}
            {activeTab === 'search' && <SearchSettingsTab />}
            {activeTab === 'indexers' && <IndexersSettings />}
            {activeTab === 'download' && <DownloadClientsSettings />}
            {activeTab === 'notifications' && <NotificationsSettings />}
            {activeTab === 'import' && <ImportSettings />}
            {activeTab === 'ui' && <UISettings />}
            {activeTab === 'security' && <SecuritySettings />}
          </div>
        </div>
      </div>
    </>
  );
}

function SettingsSection({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="card" style={{ marginBottom: '16px' }}>
      <h3 style={{ 
        fontSize: '14px', 
        fontWeight: 600, 
        color: 'var(--text-primary)',
        marginBottom: '16px',
        paddingBottom: '12px',
        borderBottom: '1px solid var(--border-color)'
      }}>
        {title}
      </h3>
      {children}
    </div>
  );
}

function SettingsField({ 
  label, 
  description, 
  children 
}: { 
  label: string; 
  description?: string;
  children: React.ReactNode;
}) {
  return (
    <div style={{ marginBottom: '16px' }}>
      <label style={{ 
        display: 'block',
        fontSize: '13px',
        fontWeight: 500,
        color: 'var(--text-primary)',
        marginBottom: '4px'
      }}>
        {label}
      </label>
      {description && (
        <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginBottom: '8px' }}>
          {description}
        </div>
      )}
      {children}
    </div>
  );
}

// Sample data for live preview
const SAMPLE_DATA = {
  seriesTitle: 'Batman',
  seriesYear: '2016',
  publisher: 'DC',
  status: 'Continuing',
  issue: '001',
  issueTitle: 'I Am Gotham',
  year: '2016',
  quality: 'Digital',
  editionType: 'TPB',
  volume: '01',
  collectionTitle: 'I Am Gotham',
};

// Replace tokens with sample values
function generatePreview(format: string): string {
  return format
    .replace(/\{Series Title\}/gi, SAMPLE_DATA.seriesTitle)
    .replace(/\{Series Year\}/gi, SAMPLE_DATA.seriesYear)
    .replace(/\{Publisher\}/gi, SAMPLE_DATA.publisher)
    .replace(/\{Status\}/gi, SAMPLE_DATA.status)
    .replace(/\{Issue\}/gi, SAMPLE_DATA.issue)
    .replace(/\{Issue Title\}/gi, SAMPLE_DATA.issueTitle)
    .replace(/\{Year\}/gi, SAMPLE_DATA.year)
    .replace(/\{Quality\}/gi, SAMPLE_DATA.quality)
    .replace(/\{Edition Type\}/gi, SAMPLE_DATA.editionType)
    .replace(/\{Volume\}/gi, SAMPLE_DATA.volume)
    .replace(/\{Collection Title\}/gi, SAMPLE_DATA.collectionTitle);
}

interface NamingToken {
  token: string;
  description: string;
  example: string;
}

interface NamingTokensResponse {
  seriesFolderTokens: NamingToken[];
  issueFileTokens: NamingToken[];
  collectionFileTokens: NamingToken[];
}

function TokenPills({ 
  tokens, 
  onTokenClick 
}: { 
  tokens: NamingToken[]; 
  onTokenClick: (token: string) => void;
}) {
  return (
    <div style={{ 
      display: 'flex', 
      flexWrap: 'wrap', 
      gap: '6px', 
      marginTop: '8px' 
    }}>
      {tokens.map((t) => (
        <button
          key={t.token}
          type="button"
          onClick={() => onTokenClick(t.token)}
          title={`${t.description} (e.g., ${t.example})`}
          style={{
            padding: '4px 8px',
            fontSize: '11px',
            fontFamily: 'var(--font-mono)',
            background: 'var(--bg-tertiary)',
            border: '1px solid var(--border-color)',
            borderRadius: 'var(--radius-sm)',
            color: 'var(--accent-primary)',
            cursor: 'pointer',
            transition: 'all 0.15s ease',
          }}
          onMouseEnter={(e) => {
            e.currentTarget.style.background = 'var(--accent-primary)';
            e.currentTarget.style.color = 'white';
          }}
          onMouseLeave={(e) => {
            e.currentTarget.style.background = 'var(--bg-tertiary)';
            e.currentTarget.style.color = 'var(--accent-primary)';
          }}
        >
          {t.token}
        </button>
      ))}
    </div>
  );
}

function FormatPreview({ format }: { format: string }) {
  const preview = generatePreview(format);
  return (
    <div style={{
      marginTop: '8px',
      padding: '8px 12px',
      background: 'var(--bg-tertiary)',
      borderRadius: 'var(--radius-sm)',
      border: '1px solid var(--border-color)',
      fontSize: '12px',
    }}>
      <span style={{ color: 'var(--text-muted)', marginRight: '8px' }}>Preview:</span>
      <span style={{ color: 'var(--text-primary)', fontFamily: 'var(--font-mono)' }}>
        {preview}
      </span>
    </div>
  );
}

function NamingFormatField({
  label,
  description,
  value,
  onChange,
  tokens,
  inputRef,
}: {
  label: string;
  description: string;
  value: string;
  onChange: (value: string) => void;
  tokens: NamingToken[];
  inputRef: React.RefObject<HTMLInputElement | null>;
}) {
  const handleTokenClick = (token: string) => {
    const input = inputRef.current;
    if (input) {
      const start = input.selectionStart ?? value.length;
      const end = input.selectionEnd ?? value.length;
      const newValue = value.slice(0, start) + token + value.slice(end);
      onChange(newValue);
      // Set cursor position after inserted token
      setTimeout(() => {
        input.focus();
        input.setSelectionRange(start + token.length, start + token.length);
      }, 0);
    } else {
      onChange(value + token);
    }
  };

  return (
    <SettingsField label={label} description={description}>
      <input
        ref={inputRef}
        className="input"
        style={{ width: '100%', fontFamily: 'var(--font-mono)', fontSize: '13px' }}
        value={value}
        onChange={(e) => onChange(e.target.value)}
      />
      <TokenPills tokens={tokens} onTokenClick={handleTokenClick} />
      <FormatPreview format={value} />
    </SettingsField>
  );
}

function GeneralSettings() {
  const [seriesFolderFormat, setSeriesFolderFormat] = useState('{Series Title} ({Year})');
  const [issueFileFormat, setIssueFileFormat] = useState('{Series Title} #{Issue} ({Year})');
  const [collectionFileFormat, setCollectionFileFormat] = useState('{Series Title} - {Edition Type} Vol. {Volume} ({Year})');
  const [saveStatus, setSaveStatus] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle');
  const saveTimeoutRef = useRef<number | null>(null);
  
  // API Key state
  const [showResetConfirm, setShowResetConfirm] = useState(false);
  const [copyFeedback, setCopyFeedback] = useState(false);
  
  const seriesInputRef = useRef<HTMLInputElement>(null);
  const issueInputRef = useRef<HTMLInputElement>(null);
  const collectionInputRef = useRef<HTMLInputElement>(null);
  const queryClient = useQueryClient();

  // Load tokens from API
  const { data: tokens } = useQuery({
    queryKey: ['namingTokens'],
    queryFn: api.getNamingTokens,
  });

  // Load saved settings from API
  const { data: generalSettings } = useQuery({
    queryKey: ['generalSettings'],
    queryFn: api.getGeneralSettings,
  });

  // Always fetch the full API key (no masking)
  const { data: apiKeyInfo, isLoading: isLoadingApiKey } = useQuery({
    queryKey: ['apiKeyFull'],
    queryFn: api.getApiKeyFull,
  });

  const resetApiKeyMutation = useMutation({
    mutationFn: api.regenerateApiKey,
    onSuccess: () => {
      setShowResetConfirm(false);
      queryClient.invalidateQueries({ queryKey: ['apiKeyFull'] });
    },
  });

  const handleCopyApiKey = async () => {
    if (apiKeyInfo?.fullKey) {
      await navigator.clipboard.writeText(apiKeyInfo.fullKey);
      setCopyFeedback(true);
      setTimeout(() => setCopyFeedback(false), 2000);
    }
  };

  // Update local state when settings load
  useEffect(() => {
    if (generalSettings) {
      setSeriesFolderFormat(generalSettings.seriesFolderFormat);
      setIssueFileFormat(generalSettings.issueFileFormat);
      setCollectionFileFormat(generalSettings.collectionFileFormat);
    }
  }, [generalSettings]);

  // Auto-save with debounce when formats change
  const saveFormats = async (series: string, issue: string, collection: string) => {
    setSaveStatus('saving');
    try {
      await api.updateGeneralSettings({
        seriesFolderFormat: series,
        issueFileFormat: issue,
        collectionFileFormat: collection,
      });
      setSaveStatus('saved');
      // Reset to idle after 2 seconds
      setTimeout(() => setSaveStatus('idle'), 2000);
    } catch (e) {
      console.error('Failed to save naming formats:', e);
      setSaveStatus('error');
      setTimeout(() => setSaveStatus('idle'), 3000);
    }
  };

  // Debounced save - triggers 500ms after last change
  const debouncedSave = (series: string, issue: string, collection: string) => {
    if (saveTimeoutRef.current) {
      clearTimeout(saveTimeoutRef.current);
    }
    saveTimeoutRef.current = window.setTimeout(() => {
      saveFormats(series, issue, collection);
    }, 500);
  };

  // Wrapper functions that update state and trigger save
  const handleSeriesFormatChange = (value: string) => {
    setSeriesFolderFormat(value);
    debouncedSave(value, issueFileFormat, collectionFileFormat);
  };

  const handleIssueFormatChange = (value: string) => {
    setIssueFileFormat(value);
    debouncedSave(seriesFolderFormat, value, collectionFileFormat);
  };

  const handleCollectionFormatChange = (value: string) => {
    setCollectionFileFormat(value);
    debouncedSave(seriesFolderFormat, issueFileFormat, value);
  };

  // Default tokens if API hasn't loaded
  const defaultTokens: NamingTokensResponse = {
    seriesFolderTokens: [
      { token: '{Series Title}', description: 'Series title', example: 'Batman' },
      { token: '{Series Year}', description: 'Year started', example: '2016' },
      { token: '{Publisher}', description: 'Publisher name', example: 'DC' },
      { token: '{Status}', description: 'Series status', example: 'Continuing' },
    ],
    issueFileTokens: [
      { token: '{Series Title}', description: 'Series title', example: 'Batman' },
      { token: '{Issue}', description: 'Issue number', example: '001' },
      { token: '{Issue Title}', description: 'Issue title', example: 'I Am Gotham' },
      { token: '{Year}', description: 'Release year', example: '2016' },
      { token: '{Publisher}', description: 'Publisher name', example: 'DC' },
      { token: '{Quality}', description: 'Quality tag', example: 'Digital' },
    ],
    collectionFileTokens: [
      { token: '{Series Title}', description: 'Series title', example: 'Batman' },
      { token: '{Edition Type}', description: 'Edition type', example: 'TPB' },
      { token: '{Volume}', description: 'Volume number', example: '01' },
      { token: '{Collection Title}', description: 'Collection title', example: 'I Am Gotham' },
      { token: '{Year}', description: 'Release year', example: '2016' },
      { token: '{Publisher}', description: 'Publisher name', example: 'DC' },
    ],
  };

  const namingTokens = tokens ?? defaultTokens;

  return (
    <>
      <SettingsSection title="Naming">
        {saveStatus !== 'idle' && (
          <div style={{
            padding: '8px 12px',
            marginBottom: '16px',
            borderRadius: 'var(--radius-sm)',
            fontSize: '12px',
            display: 'flex',
            alignItems: 'center',
            gap: '8px',
            background: saveStatus === 'saving' ? 'var(--bg-tertiary)' :
                       saveStatus === 'saved' ? 'rgba(92, 184, 92, 0.1)' :
                       'rgba(217, 83, 79, 0.1)',
            border: `1px solid ${
              saveStatus === 'saving' ? 'var(--border-color)' :
              saveStatus === 'saved' ? 'var(--accent-success)' :
              'var(--accent-danger)'
            }`,
            color: saveStatus === 'saving' ? 'var(--text-secondary)' :
                   saveStatus === 'saved' ? 'var(--accent-success)' :
                   'var(--accent-danger)',
          }}>
            {saveStatus === 'saving' && <><div className="spinner" style={{ width: '12px', height: '12px' }} /> Saving...</>}
            {saveStatus === 'saved' && <><CheckCircle size={14} /> Saved</>}
            {saveStatus === 'error' && <><AlertCircle size={14} /> Failed to save</>}
          </div>
        )}
        <NamingFormatField
          label="Series Folder Format"
          description="Pattern for organizing series folders"
          value={seriesFolderFormat}
          onChange={handleSeriesFormatChange}
          tokens={namingTokens.seriesFolderTokens}
          inputRef={seriesInputRef}
        />
        
        <NamingFormatField
          label="Issue File Format"
          description="Pattern for naming issue files"
          value={issueFileFormat}
          onChange={handleIssueFormatChange}
          tokens={namingTokens.issueFileTokens}
          inputRef={issueInputRef}
        />
        
        <NamingFormatField
          label="Collection File Format"
          description="Pattern for naming collection files"
          value={collectionFileFormat}
          onChange={handleCollectionFormatChange}
          tokens={namingTokens.collectionFileTokens}
          inputRef={collectionInputRef}
        />
      </SettingsSection>
      
      <SettingsSection title="Root Folders">
        <SettingsField 
          label="Comic Library Path" 
          description="Where your organized comics are stored"
        >
          <div style={{ display: 'flex', gap: '8px' }}>
            <input 
              className="input" 
              style={{ flex: 1 }}
              defaultValue="/comics"
            />
            <button className="btn btn-secondary">Browse</button>
          </div>
        </SettingsField>
        
        <SettingsField 
          label="Download Folder" 
          description="Where downloaded files are placed before import"
        >
          <div style={{ display: 'flex', gap: '8px' }}>
            <input 
              className="input" 
              style={{ flex: 1 }}
              defaultValue="/downloads"
            />
            <button className="btn btn-secondary">Browse</button>
          </div>
        </SettingsField>
      </SettingsSection>

      <SettingsSection title="API Key">
        <SettingsField 
          label="API Key" 
          description="API Key for external app access"
        >
          <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
            <input 
              className="input" 
              style={{ flex: 1, fontFamily: 'var(--font-mono)', fontSize: '13px', maxWidth: '400px' }}
              value={isLoadingApiKey ? 'Loading...' : (apiKeyInfo?.fullKey || '')}
              readOnly
            />
            <button 
              className="btn btn-icon" 
              onClick={handleCopyApiKey}
              title={copyFeedback ? 'Copied!' : 'Copy to clipboard'}
              disabled={isLoadingApiKey}
              style={copyFeedback ? { color: 'var(--accent-success)' } : undefined}
            >
              {copyFeedback ? <CheckCircle size={16} /> : <Copy size={16} />}
            </button>
            <button 
              className="btn btn-icon"
              onClick={() => setShowResetConfirm(true)}
              title="Reset API Key"
              disabled={isLoadingApiKey || resetApiKeyMutation.isPending}
            >
              {resetApiKeyMutation.isPending ? (
                <RefreshCw size={16} style={{ animation: 'spin 1s linear infinite' }} />
              ) : (
                <RefreshCw size={16} />
              )}
            </button>
          </div>
        </SettingsField>
      </SettingsSection>

      <LoggingSettingsSection />

      <CoverCacheSettingsSection />

      {/* Reset API Key Confirmation Modal */}
      {showResetConfirm && (
        <div style={{
          position: 'fixed',
          top: 0,
          left: 0,
          right: 0,
          bottom: 0,
          background: 'rgba(0, 0, 0, 0.6)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          zIndex: 1000,
        }}>
          <div style={{
            background: 'var(--bg-secondary)',
            borderRadius: 'var(--radius-lg)',
            padding: '24px',
            maxWidth: '400px',
            width: '90%',
            boxShadow: '0 8px 32px rgba(0, 0, 0, 0.3)',
          }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '12px', marginBottom: '16px' }}>
              <AlertCircle size={24} style={{ color: 'var(--accent-warning)' }} />
              <h3 style={{ margin: 0 }}>Reset API Key?</h3>
            </div>
            <p style={{ color: 'var(--text-secondary)', fontSize: '14px', margin: '0 0 20px 0' }}>
              This will invalidate your current API key. Any applications or integrations using
              the current key will stop working until updated with the new key.
            </p>
            <div style={{ display: 'flex', gap: '12px', justifyContent: 'flex-end' }}>
              <button 
                className="btn btn-secondary" 
                onClick={() => setShowResetConfirm(false)}
              >
                Cancel
              </button>
              <button 
                className="btn btn-primary" 
                style={{ background: 'var(--accent-warning)' }}
                onClick={() => resetApiKeyMutation.mutate()}
                disabled={resetApiKeyMutation.isPending}
              >
                {resetApiKeyMutation.isPending ? 'Resetting...' : 'Reset'}
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}

// ============== LOGGING SETTINGS ==============

const LOG_LEVELS = ['Verbose', 'Debug', 'Information', 'Warning', 'Error', 'Fatal'];

function LoggingSettingsSection() {
  const queryClient = useQueryClient();
  const [saveStatus, setSaveStatus] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle');
  
  const { data: settings, isLoading } = useQuery({
    queryKey: ['loggingSettings'],
    queryFn: api.getLoggingSettings,
  });

  const updateMutation = useMutation({
    mutationFn: api.updateLoggingSettings,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['loggingSettings'] });
      setSaveStatus('saved');
      setTimeout(() => setSaveStatus('idle'), 2000);
    },
    onError: () => {
      setSaveStatus('error');
      setTimeout(() => setSaveStatus('idle'), 3000);
    },
  });

  const [localSettings, setLocalSettings] = useState({
    logLevel: 'Information',
    logPath: '',
    maxFileSizeMb: 10,
    rotationFileCount: 5,
    consoleLoggingEnabled: true,
    sqlQueryLogging: false,
    httpRequestBodyLogging: false,
    fullStackTraces: false,
    retentionDays: 30,
    compressOldLogs: true,
    compressLogsOlderThanDays: 1,
  });

  const compressMutation = useMutation({
    mutationFn: api.triggerLogCompression,
  });

  // Update local state when settings load
  useEffect(() => {
    if (settings) {
      setLocalSettings(settings);
    }
  }, [settings]);

  const handleSave = () => {
    setSaveStatus('saving');
    updateMutation.mutate(localSettings);
  };

  if (isLoading) {
    return (
      <SettingsSection title="Logging">
        <div style={{ padding: '20px', textAlign: 'center', color: 'var(--text-secondary)' }}>
          Loading logging settings...
        </div>
      </SettingsSection>
    );
  }

  return (
    <SettingsSection title="Logging">
      {saveStatus !== 'idle' && (
        <div style={{
          padding: '8px 12px',
          marginBottom: '16px',
          borderRadius: 'var(--radius-sm)',
          fontSize: '12px',
          display: 'flex',
          alignItems: 'center',
          gap: '8px',
          background: saveStatus === 'saving' ? 'var(--bg-tertiary)' :
                     saveStatus === 'saved' ? 'rgba(92, 184, 92, 0.1)' :
                     'rgba(217, 83, 79, 0.1)',
          border: `1px solid ${
            saveStatus === 'saving' ? 'var(--border-color)' :
            saveStatus === 'saved' ? 'var(--accent-success)' :
            'var(--accent-danger)'
          }`,
          color: saveStatus === 'saving' ? 'var(--text-secondary)' :
                 saveStatus === 'saved' ? 'var(--accent-success)' :
                 'var(--accent-danger)',
        }}>
          {saveStatus === 'saving' && <><div className="spinner" style={{ width: '12px', height: '12px' }} /> Saving...</>}
          {saveStatus === 'saved' && <><CheckCircle size={14} /> Saved</>}
          {saveStatus === 'error' && <><AlertCircle size={14} /> Failed to save</>}
        </div>
      )}

      <SettingsField 
        label="Log Level" 
        description="Minimum severity level for log messages"
      >
        <select
          className="input"
          style={{ width: '200px' }}
          value={localSettings.logLevel}
          onChange={(e) => setLocalSettings({ ...localSettings, logLevel: e.target.value })}
        >
          {LOG_LEVELS.map((level) => (
            <option key={level} value={level}>{level}</option>
          ))}
        </select>
      </SettingsField>

      <SettingsField 
        label="Log File Path" 
        description="Directory where log files are stored (read-only, set via environment)"
      >
        <input
          className="input"
          style={{ width: '400px' }}
          value={localSettings.logPath}
          readOnly
          disabled
        />
      </SettingsField>

      <SettingsField 
        label="Max File Size (MB)" 
        description="Maximum size of each log file before rotation"
      >
        <input
          type="number"
          className="input"
          style={{ width: '100px' }}
          min={1}
          max={100}
          value={localSettings.maxFileSizeMb}
          onChange={(e) => setLocalSettings({ ...localSettings, maxFileSizeMb: parseInt(e.target.value) || 10 })}
        />
      </SettingsField>

      <SettingsField 
        label="Rotation File Count" 
        description="Number of rotated log files to keep"
      >
        <input
          type="number"
          className="input"
          style={{ width: '100px' }}
          min={1}
          max={20}
          value={localSettings.rotationFileCount}
          onChange={(e) => setLocalSettings({ ...localSettings, rotationFileCount: parseInt(e.target.value) || 5 })}
        />
      </SettingsField>

      <SettingsField 
        label="Log Retention (Days)" 
        description="Automatically delete logs older than this"
      >
        <input
          type="number"
          className="input"
          style={{ width: '100px' }}
          min={1}
          max={365}
          value={localSettings.retentionDays}
          onChange={(e) => setLocalSettings({ ...localSettings, retentionDays: parseInt(e.target.value) || 30 })}
        />
      </SettingsField>

      <SettingsField 
        label="Console Logging" 
        description="Also output logs to console"
      >
        <label style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer' }}>
          <input
            type="checkbox"
            checked={localSettings.consoleLoggingEnabled}
            onChange={(e) => setLocalSettings({ ...localSettings, consoleLoggingEnabled: e.target.checked })}
            style={{ width: '18px', height: '18px' }}
          />
          <span style={{ color: 'var(--text-secondary)', fontSize: '14px' }}>Enabled</span>
        </label>
      </SettingsField>

      <div style={{ marginTop: '24px', borderTop: '1px solid var(--border-color)', paddingTop: '16px' }}>
        <h4 style={{ margin: '0 0 12px 0', fontSize: '14px', color: 'var(--text-primary)' }}>
          Log Compression
        </h4>

        <SettingsField 
          label="Compress Old Logs" 
          description="Automatically compress rotated log files to save disk space"
        >
          <label style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer' }}>
            <input
              type="checkbox"
              checked={localSettings.compressOldLogs}
              onChange={(e) => setLocalSettings({ ...localSettings, compressOldLogs: e.target.checked })}
              style={{ width: '18px', height: '18px' }}
            />
            <span style={{ color: 'var(--text-secondary)', fontSize: '14px' }}>Enabled</span>
          </label>
        </SettingsField>

        <SettingsField 
          label="Compress After (Days)" 
          description="Compress logs older than this many days"
        >
          <input
            type="number"
            className="input"
            style={{ width: '100px' }}
            min={1}
            max={30}
            value={localSettings.compressLogsOlderThanDays}
            onChange={(e) => setLocalSettings({ ...localSettings, compressLogsOlderThanDays: parseInt(e.target.value) || 1 })}
            disabled={!localSettings.compressOldLogs}
          />
        </SettingsField>

        <div style={{ marginTop: '12px' }}>
          <button
            className="btn btn-secondary"
            style={{ fontSize: '13px' }}
            onClick={() => compressMutation.mutate()}
            disabled={compressMutation.isPending}
          >
            {compressMutation.isPending ? 'Compressing...' : 'Compress Now'}
          </button>
          {compressMutation.isSuccess && (
            <span style={{ marginLeft: '12px', color: 'var(--accent-success)', fontSize: '13px' }}>
              Compressed {compressMutation.data?.filesCompressed ?? 0} files, saved {((compressMutation.data?.bytesSaved ?? 0) / 1024).toFixed(1)} KB
            </span>
          )}
        </div>
      </div>

      <div style={{ marginTop: '24px', borderTop: '1px solid var(--border-color)', paddingTop: '16px' }}>
        <h4 style={{ margin: '0 0 12px 0', fontSize: '14px', color: 'var(--text-primary)' }}>
          Advanced Logging (Debug)
        </h4>

        <SettingsField 
          label="SQL Query Logging" 
          description="Log all database queries (verbose, development only)"
        >
          <label style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer' }}>
            <input
              type="checkbox"
              checked={localSettings.sqlQueryLogging}
              onChange={(e) => setLocalSettings({ ...localSettings, sqlQueryLogging: e.target.checked })}
              style={{ width: '18px', height: '18px' }}
            />
            <span style={{ color: 'var(--text-secondary)', fontSize: '14px' }}>Enabled</span>
          </label>
        </SettingsField>

        <SettingsField 
          label="HTTP Request Body Logging" 
          description="Log full HTTP request/response bodies"
        >
          <label style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer' }}>
            <input
              type="checkbox"
              checked={localSettings.httpRequestBodyLogging}
              onChange={(e) => setLocalSettings({ ...localSettings, httpRequestBodyLogging: e.target.checked })}
              style={{ width: '18px', height: '18px' }}
            />
            <span style={{ color: 'var(--text-secondary)', fontSize: '14px' }}>Enabled</span>
          </label>
        </SettingsField>

        <SettingsField 
          label="Full Stack Traces" 
          description="Include complete stack traces in error logs"
        >
          <label style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer' }}>
            <input
              type="checkbox"
              checked={localSettings.fullStackTraces}
              onChange={(e) => setLocalSettings({ ...localSettings, fullStackTraces: e.target.checked })}
              style={{ width: '18px', height: '18px' }}
            />
            <span style={{ color: 'var(--text-secondary)', fontSize: '14px' }}>Enabled</span>
          </label>
        </SettingsField>
      </div>

      <div style={{ marginTop: '20px' }}>
        <button
          className="btn btn-primary"
          onClick={handleSave}
          disabled={updateMutation.isPending}
        >
          <Save size={16} />
          {updateMutation.isPending ? 'Saving...' : 'Save Logging Settings'}
        </button>
      </div>
    </SettingsSection>
  );
}

// ============== COVER CACHE SETTINGS ==============

function CoverCacheSettingsSection() {
  const queryClient = useQueryClient();
  const [saveStatus, setSaveStatus] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle');
  
  const { data: settings, isLoading } = useQuery({
    queryKey: ['coverCacheSettings'],
    queryFn: api.getCoverCacheSettings,
  });

  const { data: cacheStats } = useQuery({
    queryKey: ['coverCacheStats'],
    queryFn: api.getCoverCacheStats,
    refetchInterval: 30000,
  });

  const { data: detailedStats } = useQuery({
    queryKey: ['detailedCoverCacheStats'],
    queryFn: api.getDetailedCoverCacheStats,
    refetchInterval: 30000,
  });

  const updateMutation = useMutation({
    mutationFn: api.updateCoverCacheSettings,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['coverCacheSettings'] });
      setSaveStatus('saved');
      setTimeout(() => setSaveStatus('idle'), 2000);
    },
    onError: () => {
      setSaveStatus('error');
      setTimeout(() => setSaveStatus('idle'), 3000);
    },
  });

  const cleanupMutation = useMutation({
    mutationFn: api.triggerCoverCacheCleanup,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['coverCacheStats'] });
      queryClient.invalidateQueries({ queryKey: ['detailedCoverCacheStats'] });
    },
  });

  const resetStatsMutation = useMutation({
    mutationFn: api.resetCoverAccessStats,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['detailedCoverCacheStats'] });
    },
  });

  const [localSettings, setLocalSettings] = useState({
    maxCacheSizeMb: 500,
    retentionDays: 0,
    cleanupTargetPercent: 80,
    cleanupIntervalHours: 24,
    autoCleanupEnabled: true,
    defaultSize: 'Medium',
    downloadAllSizes: false,
    maxConcurrentDownloads: 3,
    downloadTimeoutSeconds: 30,
    warmCacheOnSeriesAdd: false,
    warmCacheSizes: 'Medium',
    enableRevalidation: true,
    revalidationIntervalHours: 168,
  });

  useEffect(() => {
    if (settings) {
      setLocalSettings({
        maxCacheSizeMb: settings.maxCacheSizeMb,
        retentionDays: settings.retentionDays,
        cleanupTargetPercent: settings.cleanupTargetPercent,
        cleanupIntervalHours: settings.cleanupIntervalHours,
        autoCleanupEnabled: settings.autoCleanupEnabled,
        defaultSize: settings.defaultSize,
        downloadAllSizes: settings.downloadAllSizes,
        maxConcurrentDownloads: settings.maxConcurrentDownloads,
        downloadTimeoutSeconds: settings.downloadTimeoutSeconds,
        warmCacheOnSeriesAdd: settings.warmCacheOnSeriesAdd ?? false,
        warmCacheSizes: settings.warmCacheSizes ?? 'Medium',
        enableRevalidation: settings.enableRevalidation ?? true,
        revalidationIntervalHours: settings.revalidationIntervalHours ?? 168,
      });
    }
  }, [settings]);

  const handleSave = () => {
    setSaveStatus('saving');
    updateMutation.mutate(localSettings);
  };

  const formatBytes = (bytes: number) => {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return `${parseFloat((bytes / Math.pow(k, i)).toFixed(1))} ${sizes[i]}`;
  };

  if (isLoading) {
    return (
      <SettingsSection title="Cover Cache">
        <div style={{ padding: '20px', textAlign: 'center', color: 'var(--text-secondary)' }}>
          Loading cover cache settings...
        </div>
      </SettingsSection>
    );
  }

  return (
    <SettingsSection title="Cover Cache">
      {saveStatus !== 'idle' && (
        <div style={{
          padding: '8px 12px',
          marginBottom: '16px',
          borderRadius: 'var(--radius-sm)',
          fontSize: '12px',
          display: 'flex',
          alignItems: 'center',
          gap: '8px',
          background: saveStatus === 'saving' ? 'var(--bg-tertiary)' :
                     saveStatus === 'saved' ? 'rgba(92, 184, 92, 0.1)' :
                     'rgba(217, 83, 79, 0.1)',
          border: `1px solid ${
            saveStatus === 'saving' ? 'var(--border-color)' :
            saveStatus === 'saved' ? 'var(--accent-success)' :
            'var(--accent-danger)'
          }`,
        }}>
          {saveStatus === 'saving' && 'Saving changes...'}
          {saveStatus === 'saved' && <><CheckCircle size={14} /> Settings saved</>}
          {saveStatus === 'error' && <><XCircle size={14} /> Failed to save settings</>}
        </div>
      )}

      {/* Cache Statistics */}
      {cacheStats && (
        <div style={{
          background: 'var(--bg-tertiary)',
          borderRadius: 'var(--radius-md)',
          padding: '16px',
          marginBottom: '20px',
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(150px, 1fr))',
          gap: '16px',
        }}>
          <div>
            <div style={{ fontSize: '11px', color: 'var(--text-muted)', textTransform: 'uppercase' }}>Total Size</div>
            <div style={{ fontSize: '18px', fontWeight: 600, color: 'var(--text-primary)' }}>
              {formatBytes(cacheStats.totalSizeBytes)}
            </div>
          </div>
          <div>
            <div style={{ fontSize: '11px', color: 'var(--text-muted)', textTransform: 'uppercase' }}>Files</div>
            <div style={{ fontSize: '18px', fontWeight: 600, color: 'var(--text-primary)' }}>
              {cacheStats.totalCovers.toLocaleString()}
            </div>
          </div>
          <div>
            <div style={{ fontSize: '11px', color: 'var(--text-muted)', textTransform: 'uppercase' }}>Limit</div>
            <div style={{ fontSize: '18px', fontWeight: 600, color: 'var(--text-primary)' }}>
              {localSettings.maxCacheSizeMb} MB
            </div>
          </div>
          <div>
            <div style={{ fontSize: '11px', color: 'var(--text-muted)', textTransform: 'uppercase' }}>Usage</div>
            <div style={{ fontSize: '18px', fontWeight: 600, color: 
              cacheStats.totalSizeBytes > localSettings.maxCacheSizeMb * 1024 * 1024 * 0.9 ? 'var(--accent-warning)' : 'var(--accent-success)'
            }}>
              {Math.round((cacheStats.totalSizeBytes / (localSettings.maxCacheSizeMb * 1024 * 1024)) * 100)}%
            </div>
          </div>
        </div>
      )}

      {/* Access Statistics (Hit/Miss) */}
      {detailedStats?.accessStats && (
        <div style={{
          background: 'var(--bg-tertiary)',
          borderRadius: 'var(--radius-md)',
          padding: '16px',
          marginBottom: '20px',
        }}>
          <div style={{ 
            display: 'flex', 
            justifyContent: 'space-between', 
            alignItems: 'center',
            marginBottom: '12px'
          }}>
            <div style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', textTransform: 'uppercase' }}>
              Cache Performance
            </div>
            <button
              className="btn btn-secondary btn-sm"
              onClick={() => resetStatsMutation.mutate()}
              disabled={resetStatsMutation.isPending}
              title="Reset access statistics"
            >
              {resetStatsMutation.isPending ? 'Resetting...' : 'Reset Stats'}
            </button>
          </div>
          <div style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fit, minmax(130px, 1fr))',
            gap: '16px',
          }}>
            <div>
              <div style={{ fontSize: '11px', color: 'var(--text-muted)', textTransform: 'uppercase' }}>Hit Ratio</div>
              <div style={{ fontSize: '18px', fontWeight: 600, color: 
                detailedStats.accessStats.hitRatio >= 0.8 ? 'var(--accent-success)' :
                detailedStats.accessStats.hitRatio >= 0.5 ? 'var(--accent-warning)' :
                'var(--accent-danger)'
              }}>
                {(detailedStats.accessStats.hitRatio * 100).toFixed(1)}%
              </div>
            </div>
            <div>
              <div style={{ fontSize: '11px', color: 'var(--text-muted)', textTransform: 'uppercase' }}>Total Requests</div>
              <div style={{ fontSize: '18px', fontWeight: 600, color: 'var(--text-primary)' }}>
                {detailedStats.accessStats.totalRequests.toLocaleString()}
              </div>
            </div>
            <div>
              <div style={{ fontSize: '11px', color: 'var(--text-muted)', textTransform: 'uppercase' }}>Hits</div>
              <div style={{ fontSize: '18px', fontWeight: 600, color: 'var(--accent-success)' }}>
                {detailedStats.accessStats.hits.toLocaleString()}
              </div>
            </div>
            <div>
              <div style={{ fontSize: '11px', color: 'var(--text-muted)', textTransform: 'uppercase' }}>Misses</div>
              <div style={{ fontSize: '18px', fontWeight: 600, color: 'var(--accent-warning)' }}>
                {detailedStats.accessStats.misses.toLocaleString()}
              </div>
            </div>
            <div>
              <div style={{ fontSize: '11px', color: 'var(--text-muted)', textTransform: 'uppercase' }}>Fallbacks</div>
              <div style={{ fontSize: '18px', fontWeight: 600, color: 'var(--text-secondary)' }}>
                {detailedStats.accessStats.fallbacks.toLocaleString()}
              </div>
            </div>
            <div>
              <div style={{ fontSize: '11px', color: 'var(--text-muted)', textTransform: 'uppercase' }}>Bandwidth Saved</div>
              <div style={{ fontSize: '18px', fontWeight: 600, color: 'var(--accent-info)' }}>
                {formatBytes(detailedStats.accessStats.estimatedBandwidthSavedBytes)}
              </div>
            </div>
          </div>
          <div style={{ 
            marginTop: '8px', 
            fontSize: '11px', 
            color: 'var(--text-muted)'
          }}>
            Since: {new Date(detailedStats.accessStats.lastReset).toLocaleString()}
          </div>
        </div>
      )}

      <SettingsField
        label="Maximum Cache Size (MB)"
        description="Maximum disk space for cached cover images (10-10240 MB)"
      >
        <input
          type="number"
          className="input"
          style={{ width: '120px' }}
          min={10}
          max={10240}
          value={localSettings.maxCacheSizeMb}
          onChange={(e) => setLocalSettings({ ...localSettings, maxCacheSizeMb: parseInt(e.target.value) || 500 })}
        />
      </SettingsField>

      <SettingsField
        label="Retention Days"
        description="Days to keep cached covers (0 = indefinite)"
      >
        <input
          type="number"
          className="input"
          style={{ width: '120px' }}
          min={0}
          max={365}
          value={localSettings.retentionDays}
          onChange={(e) => setLocalSettings({ ...localSettings, retentionDays: parseInt(e.target.value) || 0 })}
        />
      </SettingsField>

      <SettingsField
        label="Cleanup Target (%)"
        description="When cleaning up, reduce cache to this percentage of max"
      >
        <input
          type="number"
          className="input"
          style={{ width: '120px' }}
          min={50}
          max={95}
          value={localSettings.cleanupTargetPercent}
          onChange={(e) => setLocalSettings({ ...localSettings, cleanupTargetPercent: parseInt(e.target.value) || 80 })}
        />
      </SettingsField>

      <SettingsField
        label="Cleanup Interval (Hours)"
        description="How often to run background cleanup (0 = disabled)"
      >
        <input
          type="number"
          className="input"
          style={{ width: '120px' }}
          min={0}
          max={168}
          value={localSettings.cleanupIntervalHours}
          onChange={(e) => setLocalSettings({ ...localSettings, cleanupIntervalHours: parseInt(e.target.value) || 24 })}
        />
      </SettingsField>

      <SettingsField
        label="Automatic Cleanup"
        description="Automatically clean up cache when limit is exceeded"
      >
        <label className="toggle">
          <input
            type="checkbox"
            checked={localSettings.autoCleanupEnabled}
            onChange={(e) => setLocalSettings({ ...localSettings, autoCleanupEnabled: e.target.checked })}
          />
          <span className="toggle-slider" />
        </label>
      </SettingsField>

      <SettingsField
        label="Default Cover Size"
        description="Size to download when not specified"
      >
        <select
          className="input"
          style={{ width: '150px' }}
          value={localSettings.defaultSize}
          onChange={(e) => setLocalSettings({ ...localSettings, defaultSize: e.target.value })}
        >
          <option value="Thumb">Thumb</option>
          <option value="Small">Small</option>
          <option value="Medium">Medium</option>
          <option value="Large">Large</option>
        </select>
      </SettingsField>

      <div style={{ 
        marginTop: '24px', 
        marginBottom: '16px',
        paddingTop: '16px',
        borderTop: '1px solid var(--border-color)',
      }}>
        <div style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', textTransform: 'uppercase', marginBottom: '16px' }}>
          Cache Warming
        </div>
      </div>

      <SettingsField
        label="Warm Cache on Series Add"
        description="Automatically pre-fetch covers when a series is added"
      >
        <label className="toggle">
          <input
            type="checkbox"
            checked={localSettings.warmCacheOnSeriesAdd}
            onChange={(e) => setLocalSettings({ ...localSettings, warmCacheOnSeriesAdd: e.target.checked })}
          />
          <span className="toggle-slider" />
        </label>
      </SettingsField>

      <SettingsField
        label="Warm Cache Sizes"
        description="Comma-separated list of sizes to warm (Thumb, Small, Medium, Large)"
      >
        <input
          type="text"
          className="input"
          style={{ width: '200px' }}
          value={localSettings.warmCacheSizes}
          onChange={(e) => setLocalSettings({ ...localSettings, warmCacheSizes: e.target.value })}
          placeholder="Medium,Thumb"
        />
      </SettingsField>

      <div style={{ 
        marginTop: '24px', 
        marginBottom: '16px',
        paddingTop: '16px',
        borderTop: '1px solid var(--border-color)',
      }}>
        <div style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', textTransform: 'uppercase', marginBottom: '16px' }}>
          Revalidation
        </div>
      </div>

      <SettingsField
        label="Enable Revalidation"
        description="Use ETag/Last-Modified headers to check if covers changed"
      >
        <label className="toggle">
          <input
            type="checkbox"
            checked={localSettings.enableRevalidation}
            onChange={(e) => setLocalSettings({ ...localSettings, enableRevalidation: e.target.checked })}
          />
          <span className="toggle-slider" />
        </label>
      </SettingsField>

      <SettingsField
        label="Revalidation Interval (Hours)"
        description="Hours between checking if covers have changed (0-720)"
      >
        <input
          type="number"
          className="input"
          style={{ width: '120px' }}
          min={0}
          max={720}
          value={localSettings.revalidationIntervalHours}
          onChange={(e) => setLocalSettings({ ...localSettings, revalidationIntervalHours: parseInt(e.target.value) || 168 })}
        />
      </SettingsField>

      <div style={{ marginTop: '20px', display: 'flex', gap: '12px' }}>
        <button
          className="btn btn-primary"
          onClick={handleSave}
          disabled={updateMutation.isPending}
        >
          <Save size={16} />
          {updateMutation.isPending ? 'Saving...' : 'Save Settings'}
        </button>
        <button
          className="btn btn-secondary"
          onClick={() => cleanupMutation.mutate()}
          disabled={cleanupMutation.isPending}
        >
          <RotateCcw size={16} />
          {cleanupMutation.isPending ? 'Cleaning...' : 'Run Cleanup Now'}
        </button>
      </div>
    </SettingsSection>
  );
}

// ============== COMICVINE SETTINGS ==============

function ComicVineSettingsTab() {
  const [apiKey, setApiKey] = useState('');
  const [showSavedKey, setShowSavedKey] = useState(false);
  const [fullApiKey, setFullApiKey] = useState<string | null>(null);
  const [isLoadingFullKey, setIsLoadingFullKey] = useState(false);
  const [testResult, setTestResult] = useState<ComicVineTestResult | null>(null);
  const [isTesting, setIsTesting] = useState(false);
  const queryClient = useQueryClient();

  const { data: settings, isLoading } = useQuery({
    queryKey: ['comicvineSettings'],
    queryFn: api.getComicVineSettings,
  });

  const { data: rateLimit } = useQuery({
    queryKey: ['comicvineRateLimit'],
    queryFn: api.getComicVineRateLimit,
    refetchInterval: 30000, // Refresh every 30 seconds
    enabled: settings?.hasApiKey ?? false,
  });

  const updateMutation = useMutation({
    mutationFn: api.updateComicVineSettings,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['comicvineSettings'] });
      setFullApiKey(null); // Reset cached full key after save
      setShowSavedKey(false);
    },
  });

  const handleTestConnection = async () => {
    setIsTesting(true);
    setTestResult(null);
    try {
      // If there's an unsaved key, save it first before testing
      if (apiKey.trim()) {
        await api.updateComicVineSettings({ apiKey: apiKey.trim() });
        queryClient.invalidateQueries({ queryKey: ['comicvineSettings'] });
        setApiKey('');
      }
      const result = await api.testComicVineConnection();
      setTestResult(result);
    } catch (e) {
      setTestResult({
        success: false,
        message: e instanceof Error ? e.message : 'Connection test failed',
        latencyMs: null,
        apiVersion: null,
      });
    } finally {
      setIsTesting(false);
    }
  };

  const handleSaveApiKey = () => {
    if (apiKey.trim()) {
      updateMutation.mutate({ apiKey: apiKey.trim() });
      setApiKey('');
    }
  };

  if (isLoading) {
    return <div className="loading"><div className="spinner" /></div>;
  }

  return (
    <>
      <SettingsSection title="ComicVine API">
        <SettingsField 
          label="API Key" 
          description="Specify your own ComicVine API key here. ComicVine is enabled when an API key is provided."
        >
          {settings?.hasApiKey && (
            <div style={{ 
              marginBottom: '8px',
              fontSize: '13px',
              display: 'flex',
              alignItems: 'center',
              gap: '8px'
            }}>
              <span style={{ color: 'var(--text-muted)' }}>Current key:</span>
              <code style={{ 
                fontFamily: 'var(--font-mono)', 
                background: 'var(--bg-tertiary)',
                padding: '4px 8px',
                borderRadius: 'var(--radius-sm)',
                minWidth: '150px'
              }}>
                {isLoadingFullKey ? '...' : (showSavedKey && fullApiKey ? fullApiKey : settings.maskedApiKey)}
              </code>
              <button
                className="btn btn-icon"
                onClick={async () => {
                  if (!showSavedKey && !fullApiKey) {
                    setIsLoadingFullKey(true);
                    try {
                      const result = await api.getComicVineFullApiKey();
                      setFullApiKey(result.apiKey);
                    } catch {
                      // Fall back to masked key
                    } finally {
                      setIsLoadingFullKey(false);
                    }
                  }
                  setShowSavedKey(!showSavedKey);
                }}
                title={showSavedKey ? 'Hide key' : 'Show key'}
                style={{ padding: '4px' }}
                disabled={isLoadingFullKey}
              >
                {showSavedKey ? <EyeOff size={14} /> : <Eye size={14} />}
              </button>
              {showSavedKey && fullApiKey && (
                <button
                  className="btn btn-icon"
                  onClick={() => {
                    navigator.clipboard.writeText(fullApiKey);
                  }}
                  title="Copy to clipboard"
                  style={{ padding: '4px' }}
                >
                  <Copy size={14} />
                </button>
              )}
            </div>
          )}
          <div style={{ display: 'flex', gap: '8px' }}>
            <input
              className="input"
              type="text"
              value={apiKey}
              onChange={(e) => setApiKey(e.target.value)}
              placeholder={settings?.hasApiKey ? 'Enter new key to replace' : 'Enter your ComicVine API key'}
              style={{ flex: 1 }}
            />
            <button
              className="btn btn-primary"
              onClick={handleSaveApiKey}
              disabled={!apiKey.trim() || updateMutation.isPending}
            >
              <Save size={16} />
              Save
            </button>
          </div>
          <div style={{ 
            marginTop: '8px',
            fontSize: '12px',
            color: 'var(--text-muted)'
          }}>
            Get your free API key from{' '}
            <a 
              href="https://comicvine.gamespot.com/api/" 
              target="_blank" 
              rel="noopener noreferrer"
              style={{ color: 'var(--accent-primary)', textDecoration: 'none' }}
            >
              comicvine.gamespot.com/api <ExternalLink size={12} style={{ verticalAlign: 'middle' }} />
            </a>
          </div>

          {/* Test Connection - visible when API key is entered or saved */}
          {(settings?.hasApiKey || apiKey.trim()) && (
            <div style={{ display: 'flex', gap: '12px', alignItems: 'center', marginTop: '12px' }}>
              <button
                className="btn btn-secondary"
                onClick={handleTestConnection}
                disabled={isTesting}
              >
                {isTesting ? (
                  <><div className="spinner" style={{ width: '14px', height: '14px' }} /> Testing...</>
                ) : (
                  <><Play size={16} /> Test Connection</>
                )}
              </button>
              {testResult && (
                <div style={{ 
                  display: 'flex', 
                  alignItems: 'center', 
                  gap: '8px',
                  color: testResult.success ? 'var(--accent-success)' : 'var(--accent-danger)',
                  fontSize: '13px'
                }}>
                  {testResult.success ? <CheckCircle size={16} /> : <XCircle size={16} />}
                  {testResult.message}
                  {testResult.latencyMs && <span style={{ color: 'var(--text-muted)' }}>({testResult.latencyMs}ms)</span>}
                </div>
              )}
            </div>
          )}
        </SettingsField>

      </SettingsSection>

      {settings?.hasApiKey && (
        <>
          <SettingsSection title="Rate Limit Status">
            {rateLimit ? (
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '16px' }}>
                <div style={{ padding: '16px', background: 'var(--bg-tertiary)', borderRadius: 'var(--radius-md)', textAlign: 'center' }}>
                  <div style={{ fontSize: '24px', fontWeight: 600, color: rateLimit.isRateLimited ? 'var(--accent-danger)' : 'var(--accent-primary)' }}>
                    {rateLimit.requestsUsed} / {rateLimit.requestLimit}
                  </div>
                  <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px' }}>Requests Used</div>
                </div>
                <div style={{ padding: '16px', background: 'var(--bg-tertiary)', borderRadius: 'var(--radius-md)', textAlign: 'center' }}>
                  <div style={{ fontSize: '24px', fontWeight: 600, color: 'var(--text-primary)' }}>
                    {rateLimit.requestLimit - rateLimit.requestsUsed}
                  </div>
                  <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px' }}>Remaining</div>
                </div>
                <div style={{ padding: '16px', background: 'var(--bg-tertiary)', borderRadius: 'var(--radius-md)', textAlign: 'center' }}>
                  <div style={{ fontSize: '14px', fontWeight: 500, color: 'var(--text-primary)' }}>
                    {new Date(rateLimit.windowResetTime).toLocaleTimeString()}
                  </div>
                  <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px' }}>Window Resets</div>
                </div>
              </div>
            ) : (
              <p style={{ color: 'var(--text-muted)', fontSize: '13px' }}>
                Rate limit data will be available after making API requests.
              </p>
            )}
          </SettingsSection>

          <SettingsSection title="Metadata Settings">
            <SettingsField
              label="Cache Duration"
              description="How long to cache ComicVine responses"
            >
              <select
                className="input"
                value={settings.cacheTtlHours}
                onChange={(e) => updateMutation.mutate({ cacheTtlHours: parseInt(e.target.value) })}
                style={{ width: '200px' }}
              >
                <option value={1}>1 hour</option>
                <option value={6}>6 hours</option>
                <option value={12}>12 hours</option>
                <option value={24}>24 hours (recommended)</option>
                <option value={48}>48 hours</option>
                <option value={168}>1 week</option>
              </select>
            </SettingsField>

            <SettingsField
              label="Auto-Match Threshold"
              description="Minimum confidence score for automatic series matching (0-100)"
            >
              <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                <input
                  type="range"
                  min={50}
                  max={100}
                  value={settings.autoMatchThreshold}
                  onChange={(e) => updateMutation.mutate({ autoMatchThreshold: parseInt(e.target.value) })}
                  style={{ flex: 1 }}
                />
                <span style={{ minWidth: '40px', textAlign: 'right', fontFamily: 'var(--font-mono)', fontSize: '14px' }}>
                  {settings.autoMatchThreshold}%
                </span>
              </div>
            </SettingsField>
          </SettingsSection>

          <SettingsSection title="Auto-Refresh">
            <SettingsField label="Enable Auto-Refresh" description="Automatically refresh metadata on a schedule">
              <label style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer' }}>
                <input
                  type="checkbox"
                  checked={settings.autoRefreshEnabled}
                  onChange={(e) => updateMutation.mutate({ autoRefreshEnabled: e.target.checked })}
                  style={{ width: '18px', height: '18px', cursor: 'pointer' }}
                />
                <span style={{ color: 'var(--text-secondary)', fontSize: '13px' }}>
                  {settings.autoRefreshEnabled ? 'Enabled' : 'Disabled'}
                </span>
              </label>
            </SettingsField>

            {settings.autoRefreshEnabled && (
              <SettingsField
                label="Refresh Interval"
                description="How often to refresh series metadata"
              >
                <select
                  className="input"
                  value={settings.refreshIntervalDays}
                  onChange={(e) => updateMutation.mutate({ refreshIntervalDays: parseInt(e.target.value) })}
                  style={{ width: '200px' }}
                >
                  <option value={1}>Daily</option>
                  <option value={3}>Every 3 days</option>
                  <option value={7}>Weekly (recommended)</option>
                  <option value={14}>Every 2 weeks</option>
                  <option value={30}>Monthly</option>
                </select>
              </SettingsField>
            )}
          </SettingsSection>
        </>
      )}
    </>
  );
}

// ============== SEARCH SETTINGS ==============

function SearchSettingsTab() {
  const queryClient = useQueryClient();
  const [isSaving, setIsSaving] = useState(false);
  const [isResetting, setIsResetting] = useState(false);

  const { data: settings, isLoading } = useQuery({
    queryKey: ['searchSettings'],
    queryFn: api.getSearchSettings,
  });

  const [localSettings, setLocalSettings] = useState<SearchSettings | null>(null);

  // Sync local state when data loads
  useEffect(() => {
    if (settings && !localSettings) {
      setLocalSettings(settings);
    }
  }, [settings, localSettings]);

  const handleSave = async () => {
    if (!localSettings) return;
    setIsSaving(true);
    try {
      await api.updateSearchSettings(localSettings);
      queryClient.invalidateQueries({ queryKey: ['searchSettings'] });
    } catch (error) {
      console.error('Failed to save search settings:', error);
    } finally {
      setIsSaving(false);
    }
  };

  const handleReset = async () => {
    setIsResetting(true);
    try {
      const result = await api.resetSearchSettings();
      setLocalSettings(result.settings);
      queryClient.invalidateQueries({ queryKey: ['searchSettings'] });
    } catch (error) {
      console.error('Failed to reset search settings:', error);
    } finally {
      setIsResetting(false);
    }
  };

  const updateSetting = <K extends keyof SearchSettings>(key: K, value: SearchSettings[K]) => {
    if (localSettings) {
      setLocalSettings({ ...localSettings, [key]: value });
    }
  };

  const updateListSetting = (key: 'blacklistWords' | 'whitelistWords' | 'ignoreWords' | 'formatPreference', value: string) => {
    if (localSettings) {
      setLocalSettings({ 
        ...localSettings, 
        [key]: value.split(',').map(s => s.trim()).filter(Boolean)
      });
    }
  };

  if (isLoading || !localSettings) {
    return <div className="loading"><div className="spinner" /></div>;
  }

  return (
    <>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px' }}>
        <h2 style={{ margin: 0, fontSize: '18px', fontWeight: 600 }}>Search Settings</h2>
        <div style={{ display: 'flex', gap: '8px' }}>
          <button 
            className="btn btn-secondary" 
            onClick={handleReset} 
            disabled={isResetting}
          >
            <RotateCcw size={16} />
            Reset to Defaults
          </button>
          <button 
            className="btn btn-primary" 
            onClick={handleSave} 
            disabled={isSaving}
          >
            <Save size={16} />
            {isSaving ? 'Saving...' : 'Save Settings'}
          </button>
        </div>
      </div>

      <SettingsSection title="Provider Toggles">
        <SettingsField label="Enable DDL Search" description="Search DDL providers (GetComics, ReadComicOnline)">
          <input
            type="checkbox"
            checked={localSettings.enableDdlSearch}
            onChange={e => updateSetting('enableDdlSearch', e.target.checked)}
          />
        </SettingsField>
        <SettingsField label="Enable NZB Search" description="Search Usenet/NZB indexers">
          <input
            type="checkbox"
            checked={localSettings.enableNzbSearch}
            onChange={e => updateSetting('enableNzbSearch', e.target.checked)}
          />
        </SettingsField>
        <SettingsField label="Enable Torrent Search" description="Search torrent providers (requires configuration)">
          <input
            type="checkbox"
            checked={localSettings.enableTorrentSearch}
            onChange={e => updateSetting('enableTorrentSearch', e.target.checked)}
          />
        </SettingsField>
      </SettingsSection>

      <SettingsSection title="Search Behavior">
        <SettingsField label="Search Delay (seconds)" description="Delay between consecutive searches to avoid rate limiting">
          <input
            type="number"
            value={localSettings.searchDelaySeconds}
            min={0}
            max={60}
            onChange={e => updateSetting('searchDelaySeconds', parseInt(e.target.value) || 0)}
            style={{ width: '100px' }}
          />
        </SettingsField>
        <SettingsField label="Prefer Pack Releases" description="Prioritize releases that include multiple issues">
          <input
            type="checkbox"
            checked={localSettings.preferPackReleases}
            onChange={e => updateSetting('preferPackReleases', e.target.checked)}
          />
        </SettingsField>
        <SettingsField label="Max Results Per Provider" description="Maximum results to fetch from each provider">
          <input
            type="number"
            value={localSettings.maxResultsPerProvider}
            min={1}
            max={200}
            onChange={e => updateSetting('maxResultsPerProvider', parseInt(e.target.value) || 50)}
            style={{ width: '100px' }}
          />
        </SettingsField>
        <SettingsField label="Search Tier Cutoff" description="Stop after this many providers if match found (0 = search all)">
          <input
            type="number"
            value={localSettings.searchTierCutoff}
            min={0}
            max={10}
            onChange={e => updateSetting('searchTierCutoff', parseInt(e.target.value) || 0)}
            style={{ width: '100px' }}
          />
        </SettingsField>
      </SettingsSection>

      <SettingsSection title="Quality Preferences">
        <SettingsField label="Preferred Quality" description="Preferred quality tier for releases">
          <select
            value={localSettings.preferredQuality}
            onChange={e => updateSetting('preferredQuality', parseInt(e.target.value) as PreferredQuality)}
            style={{ width: '200px' }}
          >
            <option value={0}>Any</option>
            <option value={1}>Digital (highest quality)</option>
            <option value={2}>Webrip</option>
            <option value={3}>Scan</option>
          </select>
        </SettingsField>
        <SettingsField label="CBZ Only" description="Only accept CBZ format files (reject CBR, PDF, etc.)">
          <input
            type="checkbox"
            checked={localSettings.cbzOnly}
            onChange={e => updateSetting('cbzOnly', e.target.checked)}
          />
        </SettingsField>
        <SettingsField label="Format Preference" description="Comma-separated format preference order (e.g., cbz, cbr, pdf)">
          <input
            type="text"
            value={localSettings.formatPreference.join(', ')}
            onChange={e => updateListSetting('formatPreference', e.target.value)}
            placeholder="cbz, cbr, pdf"
            style={{ width: '250px' }}
          />
        </SettingsField>
      </SettingsSection>

      <SettingsSection title="Size Limits">
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px' }}>
          <SettingsField label="Min Size (MB)" description="Minimum file size for single issues (0 = no minimum)">
            <input
              type="number"
              value={localSettings.minSizeMb}
              min={0}
              onChange={e => updateSetting('minSizeMb', parseInt(e.target.value) || 0)}
              style={{ width: '100px' }}
            />
          </SettingsField>
          <SettingsField label="Max Size (MB)" description="Maximum file size for single issues (0 = no maximum)">
            <input
              type="number"
              value={localSettings.maxSizeMb}
              min={0}
              onChange={e => updateSetting('maxSizeMb', parseInt(e.target.value) || 0)}
              style={{ width: '100px' }}
            />
          </SettingsField>
          <SettingsField label="Min Pack Size (MB)" description="Minimum size for pack/collection releases">
            <input
              type="number"
              value={localSettings.minSizePackMb}
              min={0}
              onChange={e => updateSetting('minSizePackMb', parseInt(e.target.value) || 0)}
              style={{ width: '100px' }}
            />
          </SettingsField>
          <SettingsField label="Max Pack Size (MB)" description="Maximum size for pack/collection releases">
            <input
              type="number"
              value={localSettings.maxSizePackMb}
              min={0}
              onChange={e => updateSetting('maxSizePackMb', parseInt(e.target.value) || 0)}
              style={{ width: '100px' }}
            />
          </SettingsField>
        </div>
      </SettingsSection>

      <SettingsSection title="Filtering">
        <SettingsField label="Blacklist Words" description="Comma-separated words that disqualify a release">
          <input
            type="text"
            value={localSettings.blacklistWords.join(', ')}
            onChange={e => updateListSetting('blacklistWords', e.target.value)}
            placeholder="sample, preview, watermark"
            style={{ width: '100%' }}
          />
        </SettingsField>
        <SettingsField label="Whitelist Words" description="Comma-separated required words (leave empty for no requirement)">
          <input
            type="text"
            value={localSettings.whitelistWords.join(', ')}
            onChange={e => updateListSetting('whitelistWords', e.target.value)}
            placeholder=""
            style={{ width: '100%' }}
          />
        </SettingsField>
        <SettingsField label="Ignore Words" description="Comma-separated words to strip from release names during matching">
          <input
            type="text"
            value={localSettings.ignoreWords.join(', ')}
            onChange={e => updateListSetting('ignoreWords', e.target.value)}
            placeholder="repack, proper, fixed"
            style={{ width: '100%' }}
          />
        </SettingsField>
      </SettingsSection>

      <SettingsSection title="Automation">
        <SettingsField label="Auto-Search Enabled" description="Automatically search for missing/wanted issues">
          <input
            type="checkbox"
            checked={localSettings.autoSearchEnabled}
            onChange={e => updateSetting('autoSearchEnabled', e.target.checked)}
          />
        </SettingsField>
        <SettingsField label="Auto-Search Interval (hours)" description="How often to run automatic searches">
          <input
            type="number"
            value={localSettings.autoSearchIntervalHours}
            min={1}
            max={168}
            onChange={e => updateSetting('autoSearchIntervalHours', parseInt(e.target.value) || 24)}
            style={{ width: '100px' }}
          />
        </SettingsField>
        <SettingsField label="Search New Series on Add" description="Automatically search when adding a new series">
          <input
            type="checkbox"
            checked={localSettings.searchNewSeriesOnAdd}
            onChange={e => updateSetting('searchNewSeriesOnAdd', e.target.checked)}
          />
        </SettingsField>
        <SettingsField label="Stale Search Threshold (days)" description="Re-search if not found after this many days (0 = disable)">
          <input
            type="number"
            value={localSettings.staleSearchThresholdDays}
            min={0}
            max={365}
            onChange={e => updateSetting('staleSearchThresholdDays', parseInt(e.target.value) || 0)}
            style={{ width: '100px' }}
          />
        </SettingsField>
      </SettingsSection>
    </>
  );
}

// ============== PULL LIST SETTINGS ==============

function PullListSettingsTab() {
  const queryClient = useQueryClient();
  
  const { data: settings, isLoading } = useQuery({
    queryKey: ['pulllistSettings'],
    queryFn: api.getPullListSettings,
  });

  const updateMutation = useMutation({
    mutationFn: api.updatePullListSettings,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['pulllistSettings'] });
    },
  });

  if (isLoading) {
    return <div className="loading"><div className="spinner" /></div>;
  }

  const currentSettings = settings ?? {
    weekStartDay: 0,
    releaseDay: 3,
    defaultMonitoringMode: 1,
    searchDelayHours: 6,
    autoAddToWanted: true,
    includeAnnualsInAutoAdd: true,
    includeSpecialsInAutoAdd: false,
    skipVariantCovers: true,
    upcomingWeeksToShow: 4,
    pastWeeksToShow: 4,
    enableSeriesAnnualIntegration: true,
    // Export settings
    exportWeeklyPullList: false,
    weeklyExportDirectory: null,
    weeklyExportFormat: 'Json' as const,
    autoExportOnReleaseDay: true,
    exportFields: null,
  };

  const dayOfWeekOptions = [
    { value: 0, label: 'Sunday' },
    { value: 1, label: 'Monday' },
    { value: 2, label: 'Tuesday' },
    { value: 3, label: 'Wednesday' },
    { value: 4, label: 'Thursday' },
    { value: 5, label: 'Friday' },
    { value: 6, label: 'Saturday' },
  ];

  const monitoringModeOptions = [
    { value: 0, label: 'All Issues (past and future)' },
    { value: 1, label: 'Future Issues Only' },
    { value: 2, label: 'Manual (user selects)' },
    { value: 3, label: 'First Issue Only' },
    { value: 4, label: 'None (don\'t monitor)' },
  ];

  const handleUpdate = (field: string, value: unknown) => {
    updateMutation.mutate({ ...currentSettings, [field]: value });
  };

  return (
    <>
      <SettingsSection title="Week Configuration">
        <SettingsField 
          label="Week Start Day" 
          description="The day your comic week begins (usually Sunday)"
        >
          <select
            className="input"
            value={currentSettings.weekStartDay}
            onChange={(e) => handleUpdate('weekStartDay', parseInt(e.target.value))}
            style={{ width: '200px' }}
          >
            {dayOfWeekOptions.map(opt => (
              <option key={opt.value} value={opt.value}>{opt.label}</option>
            ))}
          </select>
        </SettingsField>

        <SettingsField 
          label="Release Day" 
          description="The day comics are typically released (usually Wednesday in the US)"
        >
          <select
            className="input"
            value={currentSettings.releaseDay}
            onChange={(e) => handleUpdate('releaseDay', parseInt(e.target.value))}
            style={{ width: '200px' }}
          >
            {dayOfWeekOptions.map(opt => (
              <option key={opt.value} value={opt.value}>{opt.label}</option>
            ))}
          </select>
        </SettingsField>
      </SettingsSection>

      <SettingsSection title="Monitoring Defaults">
        <SettingsField 
          label="Default Monitoring Mode" 
          description="How new series are monitored when added"
        >
          <select
            className="input"
            value={currentSettings.defaultMonitoringMode}
            onChange={(e) => handleUpdate('defaultMonitoringMode', parseInt(e.target.value))}
            style={{ width: '300px' }}
          >
            {monitoringModeOptions.map(opt => (
              <option key={opt.value} value={opt.value}>{opt.label}</option>
            ))}
          </select>
        </SettingsField>

        <SettingsField label="Auto-Add to Wanted">
          <label style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer' }}>
            <input
              type="checkbox"
              checked={currentSettings.autoAddToWanted}
              onChange={(e) => handleUpdate('autoAddToWanted', e.target.checked)}
              style={{ width: '18px', height: '18px', cursor: 'pointer' }}
            />
            <span style={{ color: 'var(--text-secondary)', fontSize: '13px' }}>
              Automatically mark new issues as wanted based on monitoring mode
            </span>
          </label>
        </SettingsField>
      </SettingsSection>

      <SettingsSection title="Search Settings">
        <SettingsField 
          label="Search Delay (Hours)" 
          description="Hours after release day before triggering automatic searches"
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
            <input
              type="range"
              min={0}
              max={24}
              value={currentSettings.searchDelayHours}
              onChange={(e) => handleUpdate('searchDelayHours', parseInt(e.target.value))}
              style={{ flex: 1, maxWidth: '200px' }}
            />
            <span style={{ minWidth: '60px', fontFamily: 'var(--font-mono)', fontSize: '14px' }}>
              {currentSettings.searchDelayHours} hours
            </span>
          </div>
          <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px' }}>
            Waiting allows uploads to be available before searching
          </div>
        </SettingsField>
      </SettingsSection>

      <SettingsSection title="Display Settings">
        <SettingsField 
          label="Upcoming Weeks to Show" 
          description="Number of weeks shown in upcoming releases view"
        >
          <select
            className="input"
            value={currentSettings.upcomingWeeksToShow}
            onChange={(e) => handleUpdate('upcomingWeeksToShow', parseInt(e.target.value))}
            style={{ width: '120px' }}
          >
            {[2, 4, 6, 8, 12].map(n => (
              <option key={n} value={n}>{n} weeks</option>
            ))}
          </select>
        </SettingsField>

        <SettingsField 
          label="Past Weeks to Show" 
          description="Number of weeks shown in past releases view"
        >
          <select
            className="input"
            value={currentSettings.pastWeeksToShow}
            onChange={(e) => handleUpdate('pastWeeksToShow', parseInt(e.target.value))}
            style={{ width: '120px' }}
          >
            {[2, 4, 6, 8, 12].map(n => (
              <option key={n} value={n}>{n} weeks</option>
            ))}
          </select>
        </SettingsField>
      </SettingsSection>

      <SettingsSection title="Weekly Export (Mylar3 Parity)">
        <SettingsField label="Enable Weekly Export">
          <label style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer' }}>
            <input
              type="checkbox"
              checked={currentSettings.exportWeeklyPullList}
              onChange={(e) => handleUpdate('exportWeeklyPullList', e.target.checked)}
              style={{ width: '18px', height: '18px', cursor: 'pointer' }}
            />
            <span style={{ color: 'var(--text-secondary)', fontSize: '13px' }}>
              Export weekly pull list data to a file on release day
            </span>
          </label>
        </SettingsField>

        {currentSettings.exportWeeklyPullList && (
          <>
            <SettingsField 
              label="Export Directory" 
              description="Directory where weekly exports will be saved (under comics root)"
            >
              <input
                type="text"
                className="input"
                placeholder="/path/to/comics/weekly-exports"
                value={currentSettings.weeklyExportDirectory ?? ''}
                onChange={(e) => handleUpdate('weeklyExportDirectory', e.target.value || null)}
                style={{ width: '400px' }}
              />
              <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px' }}>
                Export files will be saved in subdirectories like: YYYY-WW/releases.json
              </div>
            </SettingsField>

            <SettingsField 
              label="Export Format" 
              description="File format for the exported data"
            >
              <select
                className="input"
                value={currentSettings.weeklyExportFormat}
                onChange={(e) => handleUpdate('weeklyExportFormat', e.target.value)}
                style={{ width: '200px' }}
              >
                <option value="Json">JSON (structured data)</option>
                <option value="Text">Plain Text (human readable)</option>
                <option value="Csv">CSV (spreadsheet)</option>
              </select>
            </SettingsField>

            <SettingsField label="Auto Export on Release Day">
              <label style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer' }}>
                <input
                  type="checkbox"
                  checked={currentSettings.autoExportOnReleaseDay}
                  onChange={(e) => handleUpdate('autoExportOnReleaseDay', e.target.checked)}
                  style={{ width: '18px', height: '18px', cursor: 'pointer' }}
                />
                <span style={{ color: 'var(--text-secondary)', fontSize: '13px' }}>
                  Automatically export when processing release day
                </span>
              </label>
            </SettingsField>

            <div style={{ marginTop: '16px', paddingTop: '16px', borderTop: '1px solid var(--border-color)' }}>
              <div style={{ fontSize: '13px', color: 'var(--text-secondary)', marginBottom: '12px' }}>
                Manual Export
              </div>
              <ManualExportSection />
            </div>
          </>
        )}
      </SettingsSection>
    </>
  );
}

// === Annual Handling Settings Tab ===
function AnnualHandlingSettingsTab() {
  const queryClient = useQueryClient();
  
  const { data: settings, isLoading } = useQuery({
    queryKey: ['pulllistSettings'],
    queryFn: api.getPullListSettings,
  });

  const updateMutation = useMutation({
    mutationFn: api.updatePullListSettings,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['pulllistSettings'] });
    },
  });

  if (isLoading) {
    return <div className="loading"><div className="spinner" /></div>;
  }

  const currentSettings = settings ?? {
    weekStartDay: 0,
    releaseDay: 3,
    defaultMonitoringMode: 1,
    searchDelayHours: 6,
    autoAddToWanted: true,
    includeAnnualsInAutoAdd: true,
    includeSpecialsInAutoAdd: false,
    skipVariantCovers: true,
    upcomingWeeksToShow: 4,
    pastWeeksToShow: 4,
    enableSeriesAnnualIntegration: true,
    exportWeeklyPullList: false,
    weeklyExportDirectory: null,
    weeklyExportFormat: 'Json' as const,
    autoExportOnReleaseDay: true,
    exportFields: null,
  };

  const handleUpdate = (field: string, value: unknown) => {
    updateMutation.mutate({ ...currentSettings, [field]: value });
  };

  return (
    <>
      <SettingsSection title="About Annual Issues">
        <div style={{ 
          background: 'var(--bg-tertiary)', 
          padding: '16px 20px', 
          borderRadius: 'var(--radius-md)', 
          fontSize: '13px',
          color: 'var(--text-secondary)',
          lineHeight: '1.6'
        }}>
          <strong style={{ color: 'var(--text-primary)', fontSize: '14px' }}>What are Annual Issues?</strong>
          <p style={{ margin: '10px 0 0 0' }}>
            <strong>Annuals</strong> are special yearly comic book releases that supplement a regular ongoing series. 
            For example, "Batman Annual #1" accompanies the main Batman series but is released as a special annual publication.
            These are typically larger issues with self-contained stories or important storyline events.
          </p>
          <p style={{ margin: '12px 0 0 0' }}>
            <strong>Detection:</strong> Shortboxerr automatically identifies annual issues by:
          </p>
          <ul style={{ margin: '8px 0 0 20px', padding: 0 }}>
            <li>Issue number text containing "Annual" (e.g., "Annual 1", "Annual 2024")</li>
            <li>ComicVine metadata classification</li>
            <li>Series title containing "Annual" keywords</li>
          </ul>
          <p style={{ margin: '12px 0 0 0' }}>
            <strong>Series-Annual Integration:</strong> When a regular series has associated annual releases on ComicVine, 
            Shortboxerr can automatically track them alongside the main series. Annuals will appear in your pull list 
            with the parent series and can be searched/downloaded together.
          </p>
        </div>
      </SettingsSection>

      <SettingsSection title="Series-Annual Integration">
        <SettingsField label="Enable Integration" description="Merge annual series with their parent series (Mylar3 Parity)">
          <label style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer' }}>
            <input
              type="checkbox"
              checked={currentSettings.enableSeriesAnnualIntegration ?? true}
              onChange={(e) => handleUpdate('enableSeriesAnnualIntegration', e.target.checked)}
              style={{ width: '18px', height: '18px', cursor: 'pointer' }}
            />
            <span style={{ color: 'var(--text-secondary)', fontSize: '13px' }}>
              Hide linked annual series from main series list
            </span>
          </label>
          <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '8px', marginLeft: '26px', lineHeight: '1.5' }}>
            <strong>When enabled:</strong> Annual series (e.g., "Batman Annual") will not appear as separate entries 
            in your series list. Instead, their issues will appear in the parent series' "Annuals" section.
            <br /><br />
            <strong>When disabled:</strong> Annual series will appear as separate entries in your series list, 
            just like any other series. This is useful if you prefer to manage annuals independently.
          </div>
        </SettingsField>
      </SettingsSection>

      <SettingsSection title="Include Annuals in Pull List">
        <SettingsField label="Include Annual Issues" description="Automatically add annual issues to your wanted list">
          <label style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer' }}>
            <input
              type="checkbox"
              checked={currentSettings.includeAnnualsInAutoAdd}
              onChange={(e) => handleUpdate('includeAnnualsInAutoAdd', e.target.checked)}
              style={{ width: '18px', height: '18px', cursor: 'pointer' }}
            />
            <span style={{ color: 'var(--text-secondary)', fontSize: '13px' }}>
              Include annual issues when auto-adding to wanted list
            </span>
          </label>
          <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '8px', marginLeft: '26px', lineHeight: '1.5' }}>
            When enabled, newly released annuals for your monitored series will automatically be added to your wanted list 
            based on your monitoring mode settings. Disable this if you prefer to manually select which annuals to track.
          </div>
        </SettingsField>
      </SettingsSection>

      <SettingsSection title="Special Issues">
        <SettingsField label="Include Special Issues" description="Track special one-shots, giant-size issues, and other non-standard releases">
          <label style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer' }}>
            <input
              type="checkbox"
              checked={currentSettings.includeSpecialsInAutoAdd}
              onChange={(e) => handleUpdate('includeSpecialsInAutoAdd', e.target.checked)}
              style={{ width: '18px', height: '18px', cursor: 'pointer' }}
            />
            <span style={{ color: 'var(--text-secondary)', fontSize: '13px' }}>
              Include special issues in auto-add
            </span>
          </label>
          <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '8px', marginLeft: '26px', lineHeight: '1.5' }}>
            Special issues include: Giant-Size, King-Size, One-Shot, 80-Page Giant, Specials, and other non-standard numbered issues.
            These are typically standalone stories or commemorative publications.
          </div>
        </SettingsField>
      </SettingsSection>

      <SettingsSection title="Variant Covers">
        <div style={{ 
          background: 'var(--bg-tertiary)', 
          padding: '12px 16px', 
          borderRadius: 'var(--radius-md)', 
          marginBottom: '16px',
          fontSize: '13px',
          color: 'var(--text-secondary)',
          lineHeight: '1.5'
        }}>
          <strong style={{ color: 'var(--text-primary)' }}>About Variant Covers:</strong>
          <p style={{ margin: '8px 0 0 0' }}>
            Variant covers are alternate covers for the same comic issue. A single issue like "Amazing Spider-Man #1" 
            might have 5-10+ different covers (regular, variant A, B, virgin cover, incentive covers, etc.). 
            The content inside is identical - only the cover artwork differs.
          </p>
        </div>

        <SettingsField label="Skip Variant Covers" description="Avoid downloading duplicate content with different covers">
          <label style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer' }}>
            <input
              type="checkbox"
              checked={currentSettings.skipVariantCovers}
              onChange={(e) => handleUpdate('skipVariantCovers', e.target.checked)}
              style={{ width: '18px', height: '18px', cursor: 'pointer' }}
            />
            <span style={{ color: 'var(--text-secondary)', fontSize: '13px' }}>
              Skip variant covers when auto-adding issues
            </span>
          </label>
          <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '8px', marginLeft: '26px', lineHeight: '1.5' }}>
            Recommended for most users. Enable this to only track the main cover version of each issue.
            Disable if you're a collector who wants to track specific variant covers.
          </div>
        </SettingsField>
      </SettingsSection>

      {(currentSettings.enableSeriesAnnualIntegration ?? true) && (
        <SettingsSection title="Link Existing Annual Series">
          <AnnualLinkingSection />
        </SettingsSection>
      )}

      <SettingsSection title="Per-Series Overrides">
        <div style={{ 
          background: 'var(--bg-secondary)', 
          border: '1px solid var(--border-color)',
          padding: '16px 20px', 
          borderRadius: 'var(--radius-md)', 
          fontSize: '13px',
          color: 'var(--text-secondary)',
          lineHeight: '1.5'
        }}>
          <strong style={{ color: 'var(--text-primary)' }}>Customizing Individual Series</strong>
          <p style={{ margin: '10px 0 0 0' }}>
            The settings above control the <em>global default</em> behavior for all series.
          </p>
          <p style={{ margin: '10px 0 0 0' }}>
            To customize annual/special issue handling for a specific series:
          </p>
          <ol style={{ margin: '8px 0 0 20px', padding: 0 }}>
            <li>Navigate to the series detail page</li>
            <li>Click the settings icon (gear) in the header</li>
            <li>Adjust the annual/special issue settings for that series</li>
          </ol>
          <p style={{ margin: '10px 0 0 0', fontStyle: 'italic' }}>
            Per-series settings override the global defaults configured here.
          </p>
        </div>
      </SettingsSection>
    </>
  );
}

// Annual Linking Section Component - links existing annual series to parents
function AnnualLinkingSection() {
  const [isLinking, setIsLinking] = useState(false);
  const [result, setResult] = useState<{
    success: boolean;
    linkedCount: number;
    totalScanned: number;
    links: { annualSeriesTitle: string; parentSeriesTitle: string }[];
    unlinkedAnnuals: { title: string; expectedParentName: string }[];
  } | null>(null);
  const queryClient = useQueryClient();

  const handleLinkAnnuals = async () => {
    setIsLinking(true);
    setResult(null);
    try {
      const response = await api.linkExistingAnnualSeries();
      setResult({
        success: response.success,
        linkedCount: response.linkedCount,
        totalScanned: response.totalScanned,
        links: response.links,
        unlinkedAnnuals: response.unlinkedAnnuals,
      });
      // Invalidate series queries to refresh data
      queryClient.invalidateQueries({ queryKey: ['series'] });
      queryClient.invalidateQueries({ queryKey: ['seriesDetail'] });
    } catch {
      setResult({
        success: false,
        linkedCount: 0,
        totalScanned: 0,
        links: [],
        unlinkedAnnuals: [],
      });
    } finally {
      setIsLinking(false);
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
      <div style={{ 
        background: 'var(--bg-tertiary)', 
        padding: '16px 20px', 
        borderRadius: 'var(--radius-md)', 
        fontSize: '13px',
        color: 'var(--text-secondary)',
        lineHeight: '1.6'
      }}>
        <strong style={{ color: 'var(--text-primary)', fontSize: '14px' }}>Update Existing Library</strong>
        <p style={{ margin: '10px 0 0 0' }}>
          If you have series in your library that were added before the annual linking feature, you can scan
          and link them now. This will:
        </p>
        <ul style={{ margin: '8px 0 0 20px', padding: 0 }}>
          <li>Scan all series with "Annual" in their title</li>
          <li>Automatically link them to their parent series (e.g., "Batman Annual" &rarr; "Batman")</li>
          <li>Display the linked annuals in the parent series' detail page</li>
        </ul>
        <p style={{ margin: '12px 0 0 0', fontStyle: 'italic' }}>
          This process is safe to run multiple times - it will only link series that aren't already linked.
        </p>
      </div>

      <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
        <button
          onClick={handleLinkAnnuals}
          disabled={isLinking}
          style={{
            padding: '10px 20px',
            background: isLinking ? 'var(--bg-tertiary)' : 'var(--accent-primary)',
            color: isLinking ? 'var(--text-muted)' : '#fff',
            border: 'none',
            borderRadius: 'var(--radius-md)',
            cursor: isLinking ? 'not-allowed' : 'pointer',
            fontSize: '14px',
            fontWeight: 500,
            display: 'flex',
            alignItems: 'center',
            gap: '8px',
          }}
        >
          {isLinking && <span className="spinner" style={{ width: '16px', height: '16px' }} />}
          {isLinking ? 'Scanning Library...' : 'Link Existing Annual Series'}
        </button>
      </div>

      {result && (
        <div style={{
          background: result.success ? 'rgba(16, 185, 129, 0.1)' : 'rgba(239, 68, 68, 0.1)',
          border: `1px solid ${result.success ? 'rgba(16, 185, 129, 0.3)' : 'rgba(239, 68, 68, 0.3)'}`,
          borderRadius: 'var(--radius-md)',
          padding: '16px 20px',
        }}>
          <div style={{ 
            fontSize: '14px', 
            fontWeight: 600, 
            color: result.success ? 'var(--accent-success)' : 'var(--accent-danger)',
            marginBottom: result.linkedCount > 0 || result.unlinkedAnnuals.length > 0 ? '12px' : 0,
          }}>
            {result.success 
              ? `Scanned ${result.totalScanned} series, linked ${result.linkedCount} annual series`
              : 'Failed to link annual series'}
          </div>
          
          {result.links.length > 0 && (
            <div style={{ marginBottom: result.unlinkedAnnuals.length > 0 ? '12px' : 0 }}>
              <div style={{ fontSize: '13px', fontWeight: 500, color: 'var(--text-primary)', marginBottom: '8px' }}>
                Successfully Linked:
              </div>
              <ul style={{ margin: 0, padding: '0 0 0 20px', fontSize: '13px', color: 'var(--text-secondary)' }}>
                {result.links.map((link, i) => (
                  <li key={i}>{link.annualSeriesTitle} &rarr; {link.parentSeriesTitle}</li>
                ))}
              </ul>
            </div>
          )}
          
          {result.unlinkedAnnuals.length > 0 && (
            <div>
              <div style={{ fontSize: '13px', fontWeight: 500, color: 'var(--text-primary)', marginBottom: '8px' }}>
                Unlinked (parent series not in library):
              </div>
              <ul style={{ margin: 0, padding: '0 0 0 20px', fontSize: '13px', color: 'var(--text-muted)' }}>
                {result.unlinkedAnnuals.slice(0, 10).map((annual, i) => (
                  <li key={i}>{annual.title} (expected parent: {annual.expectedParentName})</li>
                ))}
                {result.unlinkedAnnuals.length > 10 && (
                  <li style={{ fontStyle: 'italic' }}>...and {result.unlinkedAnnuals.length - 10} more</li>
                )}
              </ul>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

// Manual Export Section Component
function ManualExportSection() {
  const [exporting, setExporting] = useState(false);
  const [exportResult, setExportResult] = useState<{ success: boolean; message: string } | null>(null);

  const handleExport = async () => {
    setExporting(true);
    setExportResult(null);
    try {
      const result = await api.exportCurrentWeek();
      if (result.success) {
        setExportResult({
          success: true,
          message: `Exported ${result.totalIssues} issues to ${result.exportFilePath}`
        });
      } else {
        setExportResult({
          success: false,
          message: result.error ?? 'Export failed'
        });
      }
    } catch (err) {
      setExportResult({
        success: false,
        message: 'Failed to export: ' + (err instanceof Error ? err.message : 'Unknown error')
      });
    } finally {
      setExporting(false);
    }
  };

  return (
    <div>
      <button 
        className="btn btn-secondary"
        onClick={handleExport}
        disabled={exporting}
        style={{ marginRight: '12px' }}
      >
        {exporting ? (
          <>
            <RefreshCw size={16} className="spin" style={{ marginRight: '8px' }} />
            Exporting...
          </>
        ) : (
          <>
            <FileText size={16} style={{ marginRight: '8px' }} />
            Export This Week
          </>
        )}
      </button>
      
      {exportResult && (
        <div style={{
          marginTop: '12px',
          padding: '12px',
          borderRadius: 'var(--radius-md)',
          backgroundColor: exportResult.success 
            ? 'rgba(34, 197, 94, 0.1)' 
            : 'rgba(239, 68, 68, 0.1)',
          color: exportResult.success 
            ? 'var(--accent-success)' 
            : 'var(--accent-danger)',
          fontSize: '13px'
        }}>
          {exportResult.message}
        </div>
      )}
    </div>
  );
}

// ============== INDEXERS SETTINGS ==============

function IndexersSettings() {
  const [showModal, setShowModal] = useState(false);
  const [editingProvider, setEditingProvider] = useState<Provider | null>(null);
  const [showNzbIndexerModal, setShowNzbIndexerModal] = useState(false);
  const [editingNzbIndexer, setEditingNzbIndexer] = useState<NzbIndexer | null>(null);
  const queryClient = useQueryClient();

  // Only fetch RSS indexers (DDL indexers are built-in)
  const { data: indexers, isLoading, refetch } = useQuery({
    queryKey: ['indexers'],
    queryFn: api.getIndexers,
  });

  // NZB Indexers
  const { data: nzbIndexersResponse, isLoading: nzbIndexersLoading, refetch: refetchNzbIndexers } = useQuery({
    queryKey: ['nzbIndexers'],
    queryFn: api.getNzbIndexers,
  });

  const { data: nzbPresets } = useQuery({
    queryKey: ['nzbIndexerPresets'],
    queryFn: api.getNzbIndexerPresets,
  });

  const toggleMutation = useMutation({
    mutationFn: ({ id, enabled }: { id: number; enabled: boolean }) => 
      api.setProviderEnabled(id, enabled),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['indexers'] }),
  });

  const deleteMutation = useMutation({
    mutationFn: api.deleteProvider,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['indexers'] }),
  });

  const deleteNzbIndexerMutation = useMutation({
    mutationFn: api.deleteNzbIndexer,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['nzbIndexers'] }),
  });

  const handleAdd = () => {
    setEditingProvider(null);
    setShowModal(true);
  };

  const handleEdit = (provider: Provider) => {
    setEditingProvider(provider);
    setShowModal(true);
  };

  const handleDelete = (provider: Provider) => {
    if (confirm(`Delete indexer "${provider.name}"? This cannot be undone.`)) {
      deleteMutation.mutate(provider.id);
    }
  };

  const handleModalClose = () => {
    setShowModal(false);
    setEditingProvider(null);
  };

  const handleModalSave = () => {
    queryClient.invalidateQueries({ queryKey: ['indexers'] });
    handleModalClose();
  };

  // NZB Indexer handlers
  const handleAddNzbIndexer = () => {
    setEditingNzbIndexer(null);
    setShowNzbIndexerModal(true);
  };

  const handleEditNzbIndexer = (indexer: NzbIndexer) => {
    setEditingNzbIndexer(indexer);
    setShowNzbIndexerModal(true);
  };

  const handleDeleteNzbIndexer = (indexer: NzbIndexer) => {
    if (confirm(`Delete indexer "${indexer.name}"? This cannot be undone.`)) {
      deleteNzbIndexerMutation.mutate(indexer.id);
    }
  };

  const handleNzbModalClose = () => {
    setShowNzbIndexerModal(false);
    setEditingNzbIndexer(null);
  };

  const handleNzbModalSave = () => {
    queryClient.invalidateQueries({ queryKey: ['nzbIndexers'] });
    handleNzbModalClose();
  };

  return (
    <>
      <DdlSitesSection />

      <SettingsSection title="Usenet / NZB Indexers">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
          <p style={{ color: 'var(--text-muted)', fontSize: '13px', margin: 0 }}>
            Configure Newznab-compatible NZB indexers for Usenet search.
          </p>
          <div style={{ display: 'flex', gap: '8px' }}>
            <button className="btn btn-icon" onClick={() => refetchNzbIndexers()} title="Refresh">
              <RefreshCw size={16} />
            </button>
            <button className="btn btn-primary" onClick={handleAddNzbIndexer}>
              <Plus size={16} />
              Add NZB Indexer
            </button>
          </div>
        </div>

        {nzbIndexersLoading ? (
          <div className="loading"><div className="spinner" /></div>
        ) : !nzbIndexersResponse?.indexers?.length ? (
          <div className="empty-state" style={{ padding: '40px 20px' }}>
            <HardDrive size={48} />
            <div className="empty-state-title">No NZB indexers configured</div>
            <div className="empty-state-text">
              Add Newznab indexers like NZBgeek, DrunkenSlug, or others.
            </div>
          </div>
        ) : (
          <NzbIndexerTable
            indexers={nzbIndexersResponse.indexers}
            onEdit={handleEditNzbIndexer}
            onDelete={handleDeleteNzbIndexer}
          />
        )}

        {nzbIndexersResponse && nzbIndexersResponse.indexers?.length > 0 && (
          <div style={{ marginTop: '12px', fontSize: '12px', color: 'var(--text-muted)' }}>
            {nzbIndexersResponse.enabledCount} of {nzbIndexersResponse.totalCount} indexers enabled
          </div>
        )}
      </SettingsSection>

      <SettingsSection title="RSS Feeds">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
          <p style={{ color: 'var(--text-muted)', fontSize: '13px', margin: 0 }}>
            Add custom RSS/Atom feeds to discover new releases.
          </p>
          <div style={{ display: 'flex', gap: '8px' }}>
            <button className="btn btn-icon" onClick={() => refetch()} title="Refresh">
              <RefreshCw size={16} />
            </button>
            <button className="btn btn-primary" onClick={handleAdd}>
              <Plus size={16} />
              Add RSS Feed
            </button>
          </div>
        </div>

        {isLoading ? (
          <div className="loading"><div className="spinner" /></div>
        ) : !indexers?.length ? (
          <div className="empty-state" style={{ padding: '40px 20px' }}>
            <Plug size={48} />
            <div className="empty-state-title">No RSS feeds configured</div>
            <div className="empty-state-text">
              Add RSS feeds to discover new comic releases.
            </div>
          </div>
        ) : (
          <ProviderTable
            providers={indexers}
            onToggle={(id, enabled) => toggleMutation.mutate({ id, enabled })}
            onEdit={handleEdit}
            onDelete={handleDelete}
          />
        )}
      </SettingsSection>

      {showModal && (
        <ProviderModal
          provider={editingProvider}
          category="Indexer"
          onClose={handleModalClose}
          onSave={handleModalSave}
        />
      )}

      {showNzbIndexerModal && (
        <NzbIndexerModal
          indexer={editingNzbIndexer}
          presets={nzbPresets?.presets ?? []}
          onClose={handleNzbModalClose}
          onSave={handleNzbModalSave}
        />
      )}
    </>
  );
}

// DDL Site Management Section
interface DdlSite {
  siteType: string;
  displayName: string;
  defaultBaseUrl: string;
  requiresAuthentication: boolean;
  defaultRateLimitPerMinute: number;
  isEnabled: boolean;
  priority: number;
  health: string;
  lastError?: string;
  lastSuccessfulSearch?: string;
}

interface DdlTestResult {
  siteType: string;
  success: boolean;
  message: string;
  sampleResultCount: number;
  latencyMs: number;
}

function DdlSitesSection() {
  const queryClient = useQueryClient();
  const [testingId, setTestingId] = useState<string | null>(null);
  const [testResult, setTestResult] = useState<DdlTestResult | null>(null);

  const { data: sites, isLoading, refetch } = useQuery<DdlSite[]>({
    queryKey: ['ddlSites'],
    queryFn: async () => {
      const response = await fetch('/api/v1/ddl/sites');
      if (!response.ok) throw new Error('Failed to fetch DDL sites');
      return response.json();
    },
  });

  const toggleMutation = useMutation({
    mutationFn: async ({ siteType, enable }: { siteType: string; enable: boolean }) => {
      const response = await fetch(`/api/v1/ddl/sites/${siteType}/${enable ? 'enable' : 'disable'}`, {
        method: 'POST',
      });
      if (!response.ok) throw new Error('Failed to toggle site');
      return response.json();
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['ddlSites'] });
    },
  });

  const testSite = async (siteType: string) => {
    setTestingId(siteType);
    setTestResult(null);
    try {
      const response = await fetch(`/api/v1/ddl/sites/${siteType}/test`, { method: 'POST' });
      const result = await response.json();
      setTestResult(result);
    } catch (error) {
      setTestResult({
        siteType,
        success: false,
        message: 'Test failed: ' + (error instanceof Error ? error.message : 'Unknown error'),
        sampleResultCount: 0,
        latencyMs: 0,
      });
    } finally {
      setTestingId(null);
    }
  };

  const getSiteDescription = (siteType: string): string => {
    switch (siteType) {
      case 'GetComics':
        return 'Primary DDL source with comprehensive comic releases';
      case 'ReadComicOnline':
        return 'Comic reading site with download links';
      case 'MockDdl':
        return 'Test/development adapter';
      case 'GettyComics':
        return 'Legacy test adapter';
      default:
        return 'DDL site adapter';
    }
  };

  return (
    <SettingsSection title="DDL Sites (Built-in)">
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
        <p style={{ color: 'var(--text-muted)', fontSize: '13px', margin: 0 }}>
          DDL indexers are built-in with Mylar3 parity. Enable/disable sites and test connectivity.
        </p>
        <button className="btn btn-icon" onClick={() => refetch()} title="Refresh">
          <RefreshCw size={16} />
        </button>
      </div>

      {isLoading ? (
        <div className="loading"><div className="spinner" /></div>
      ) : !sites?.length ? (
        <div className="empty-state" style={{ padding: '40px 20px' }}>
          <Globe size={48} />
          <div className="empty-state-title">No DDL sites available</div>
        </div>
      ) : (
        <>
          <div style={{ 
            display: 'grid', 
            gridTemplateColumns: 'repeat(auto-fill, minmax(320px, 1fr))', 
            gap: '12px' 
          }}>
            {sites
              .filter(site => site.siteType !== 'MockDdl' && site.siteType !== 'GettyComics')
              .map((site) => (
                <DdlSiteCard 
                  key={site.siteType}
                  site={site}
                  description={getSiteDescription(site.siteType)}
                  onToggle={(enable) => toggleMutation.mutate({ siteType: site.siteType, enable })}
                  onTest={() => testSite(site.siteType)}
                  isTesting={testingId === site.siteType}
                  isToggling={toggleMutation.isPending}
                />
              ))}
          </div>
          
          {testResult && (
            <div style={{
              marginTop: '16px',
              padding: '12px 16px',
              borderRadius: 'var(--radius-md)',
              background: testResult.success ? 'rgba(92, 184, 92, 0.1)' : 'rgba(217, 83, 79, 0.1)',
              border: `1px solid ${testResult.success ? 'var(--accent-success)' : 'var(--accent-danger)'}`,
            }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '4px' }}>
                {testResult.success ? (
                  <CheckCircle size={16} style={{ color: 'var(--accent-success)' }} />
                ) : (
                  <XCircle size={16} style={{ color: 'var(--accent-danger)' }} />
                )}
                <span style={{ fontWeight: 500 }}>{testResult.siteType} Test {testResult.success ? 'Passed' : 'Failed'}</span>
              </div>
              <div style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>
                {testResult.message}
                {testResult.success && (
                  <span style={{ marginLeft: '8px', color: 'var(--text-muted)' }}>
                    ({testResult.latencyMs}ms)
                  </span>
                )}
              </div>
            </div>
          )}

          <div style={{ marginTop: '12px', fontSize: '12px', color: 'var(--text-muted)' }}>
            {sites.filter(s => s.isEnabled && s.siteType !== 'MockDdl').length} of {sites.filter(s => s.siteType !== 'MockDdl' && s.siteType !== 'GettyComics').length} sites enabled
          </div>
        </>
      )}
    </SettingsSection>
  );
}

function DdlSiteCard({ 
  site, 
  description, 
  onToggle, 
  onTest, 
  isTesting,
  isToggling
}: { 
  site: DdlSite; 
  description: string; 
  onToggle: (enable: boolean) => void;
  onTest: () => void;
  isTesting: boolean;
  isToggling: boolean;
}) {
  return (
    <div style={{
      padding: '16px',
      background: 'var(--bg-tertiary)',
      borderRadius: 'var(--radius-md)',
      border: `1px solid ${site.isEnabled ? 'var(--accent-success)' : 'var(--border-color)'}`,
    }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '8px' }}>
        <div>
          <div style={{ fontWeight: 500, color: 'var(--text-primary)' }}>{site.displayName}</div>
          <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>Priority: {site.priority}</div>
        </div>
        <div style={{ 
          fontSize: '11px', 
          padding: '2px 8px', 
          borderRadius: 'var(--radius-sm)',
          background: site.isEnabled ? 'rgba(92, 184, 92, 0.2)' : 'rgba(150, 150, 150, 0.2)',
          color: site.isEnabled ? 'var(--accent-success)' : 'var(--text-muted)',
        }}>
          {site.isEnabled ? 'Enabled' : 'Disabled'}
        </div>
      </div>
      <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginBottom: '12px' }}>{description}</div>
      <div style={{ fontSize: '11px', color: 'var(--text-muted)', marginBottom: '12px' }}>
        Rate limit: {site.defaultRateLimitPerMinute} req/min
      </div>
      <div style={{ display: 'flex', gap: '8px' }}>
        <button 
          className="btn btn-secondary" 
          onClick={onTest}
          disabled={isTesting}
          style={{ flex: 1 }}
        >
          {isTesting ? <Loader2 size={14} className="spin" /> : <Activity size={14} />}
          Test
        </button>
        <button 
          className={`btn ${site.isEnabled ? 'btn-secondary' : 'btn-primary'}`}
          onClick={() => onToggle(!site.isEnabled)}
          disabled={isToggling}
          style={{ flex: 1 }}
        >
          {site.isEnabled ? 'Disable' : 'Enable'}
        </button>
      </div>
    </div>
  );
}

// ============== NZB / USENET HELPER COMPONENTS ==============

function NzbIndexerTable({
  indexers,
  onEdit,
  onDelete,
}: {
  indexers: NzbIndexer[];
  onEdit: (indexer: NzbIndexer) => void;
  onDelete: (indexer: NzbIndexer) => void;
}) {
  const [testingId, setTestingId] = useState<string | null>(null);
  const [testResult, setTestResult] = useState<{ id: string; result: NzbTestResult } | null>(null);

  const handleTest = async (indexer: NzbIndexer) => {
    setTestingId(indexer.id);
    setTestResult(null);
    try {
      const result = await api.testNzbIndexer(indexer.id);
      setTestResult({ id: indexer.id, result });
    } catch (e) {
      setTestResult({
        id: indexer.id,
        result: { success: false, message: 'Test failed: ' + String(e) }
      });
    } finally {
      setTestingId(null);
    }
  };

  return (
    <div className="table-container">
      <table className="table">
        <thead>
          <tr>
            <th>Name</th>
            <th>URL</th>
            <th>Priority</th>
            <th>Status</th>
            <th className="table-actions">Actions</th>
          </tr>
        </thead>
        <tbody>
          {indexers.map((indexer) => (
            <tr key={indexer.id}>
              <td>
                <div style={{ fontWeight: 500, color: 'var(--text-primary)' }}>{indexer.name}</div>
              </td>
              <td>
                <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
                  {indexer.baseUrl}
                </div>
              </td>
              <td>{indexer.priority}</td>
              <td>
                <span className={`badge badge-${indexer.enabled ? 'success' : 'muted'}`}>
                  {indexer.enabled ? 'Enabled' : 'Disabled'}
                </span>
                {testResult?.id === indexer.id && (
                  <div style={{ marginTop: '4px' }}>
                    <span className={`badge badge-${testResult.result.success ? 'success' : 'danger'}`}>
                      {testResult.result.success ? 'Connected' : 'Failed'}
                    </span>
                  </div>
                )}
              </td>
              <td className="table-actions">
                <button
                  className="btn btn-icon"
                  onClick={() => handleTest(indexer)}
                  disabled={testingId === indexer.id}
                  title="Test"
                >
                  {testingId === indexer.id ? (
                    <div className="spinner" style={{ width: '16px', height: '16px' }} />
                  ) : (
                    <Play size={16} />
                  )}
                </button>
                <button className="btn btn-icon" onClick={() => onEdit(indexer)} title="Edit">
                  <Edit size={16} />
                </button>
                <button className="btn btn-icon" onClick={() => onDelete(indexer)} title="Delete">
                  <Trash2 size={16} />
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function NzbIndexerModal({
  indexer,
  presets,
  onClose,
  onSave,
}: {
  indexer: NzbIndexer | null;
  presets: NzbIndexerPreset[];
  onClose: () => void;
  onSave: () => void;
}) {
  const [formData, setFormData] = useState<NzbIndexerRequest>({
    name: indexer?.name ?? '',
    baseUrl: indexer?.baseUrl ?? '',
    apiKey: indexer?.apiKey ?? '',
    enabled: indexer?.enabled ?? true,
    priority: indexer?.priority ?? 50,
    categories: indexer?.categories ?? [7030, 7000],
  });
  const [selectedPreset, setSelectedPreset] = useState<string>('');
  const [testing, setTesting] = useState(false);
  const [testResult, setTestResult] = useState<NzbTestResult | null>(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handlePresetSelect = (presetId: string) => {
    setSelectedPreset(presetId);
    const preset = presets.find(p => p.id === presetId);
    if (preset) {
      setFormData({
        ...formData,
        name: preset.name,
        baseUrl: preset.baseUrl,
        categories: preset.defaultCategories,
      });
    }
  };

  const handleTest = async () => {
    setTesting(true);
    setTestResult(null);
    try {
      const result = await api.testNzbIndexerConfig({
        baseUrl: formData.baseUrl ?? '',
        apiKey: formData.apiKey ?? '',
      });
      setTestResult(result);
    } catch (e) {
      setTestResult({ success: false, message: 'Test failed: ' + String(e) });
    } finally {
      setTesting(false);
    }
  };

  const handleSave = async () => {
    setSaving(true);
    setError(null);
    try {
      if (indexer) {
        await api.updateNzbIndexer(indexer.id, formData);
      } else {
        await api.addNzbIndexer(formData);
      }
      onSave();
    } catch (e) {
      setError(String(e));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div style={{
      position: 'fixed',
      inset: 0,
      background: 'rgba(0, 0, 0, 0.7)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      zIndex: 1000,
    }}>
      <div style={{
        background: 'var(--bg-secondary)',
        borderRadius: 'var(--radius-lg)',
        border: '1px solid var(--border-color)',
        width: '100%',
        maxWidth: '500px',
        maxHeight: '90vh',
        overflow: 'auto',
      }}>
        <div style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          padding: '16px 20px',
          borderBottom: '1px solid var(--border-color)',
        }}>
          <h2 style={{ margin: 0, fontSize: '16px', fontWeight: 600 }}>
            {indexer ? 'Edit NZB Indexer' : 'Add NZB Indexer'}
          </h2>
          <button className="btn btn-icon" onClick={onClose}>
            <X size={18} />
          </button>
        </div>

        <div style={{ padding: '20px' }}>
          {!indexer && presets.length > 0 && (
            <SettingsField label="Preset" description="Start with a pre-configured indexer">
              <select
                className="input"
                style={{ width: '100%' }}
                value={selectedPreset}
                onChange={(e) => handlePresetSelect(e.target.value)}
              >
                <option value="">Custom / Manual</option>
                {presets.map((preset) => (
                  <option key={preset.id} value={preset.id}>{preset.name}</option>
                ))}
              </select>
            </SettingsField>
          )}

          <SettingsField label="Name">
            <input
              className="input"
              style={{ width: '100%' }}
              value={formData.name}
              onChange={(e) => setFormData({ ...formData, name: e.target.value })}
              placeholder="My Indexer"
            />
          </SettingsField>

          <SettingsField label="URL" description="Newznab API base URL">
            <input
              className="input"
              style={{ width: '100%' }}
              value={formData.baseUrl}
              onChange={(e) => setFormData({ ...formData, baseUrl: e.target.value })}
              placeholder="https://api.indexer.com"
            />
          </SettingsField>

          <SettingsField label="API Key">
            <input
              className="input"
              style={{ width: '100%' }}
              value={formData.apiKey}
              onChange={(e) => setFormData({ ...formData, apiKey: e.target.value })}
              placeholder="Your API key"
              type="password"
            />
          </SettingsField>

          <SettingsField label="Priority" description="Lower number = higher priority (1-100)">
            <input
              className="input"
              style={{ width: '100px' }}
              type="number"
              min={1}
              max={100}
              value={formData.priority}
              onChange={(e) => setFormData({ ...formData, priority: parseInt(e.target.value) || 50 })}
            />
          </SettingsField>

          <SettingsField label="Enabled">
            <label style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <input
                type="checkbox"
                checked={formData.enabled}
                onChange={(e) => setFormData({ ...formData, enabled: e.target.checked })}
              />
              <span style={{ fontSize: '13px' }}>Enable this indexer</span>
            </label>
          </SettingsField>

          {testResult && (
            <div style={{
              padding: '12px',
              borderRadius: 'var(--radius-md)',
              background: testResult.success ? 'rgba(92, 184, 92, 0.1)' : 'rgba(217, 83, 79, 0.1)',
              border: `1px solid ${testResult.success ? 'var(--accent-success)' : 'var(--accent-danger)'}`,
              marginBottom: '16px',
            }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                {testResult.success ? (
                  <CheckCircle size={16} style={{ color: 'var(--accent-success)' }} />
                ) : (
                  <AlertCircle size={16} style={{ color: 'var(--accent-danger)' }} />
                )}
                <span style={{ fontWeight: 500, color: testResult.success ? 'var(--accent-success)' : 'var(--accent-danger)' }}>
                  {testResult.success ? 'Connection successful' : 'Connection failed'}
                </span>
              </div>
              {testResult.message && (
                <div style={{ fontSize: '13px', color: 'var(--text-secondary)', marginTop: '4px' }}>
                  {testResult.message}
                </div>
              )}
            </div>
          )}

          {error && (
            <div style={{
              padding: '12px',
              borderRadius: 'var(--radius-md)',
              background: 'rgba(217, 83, 79, 0.1)',
              border: '1px solid var(--accent-danger)',
              color: 'var(--accent-danger)',
              fontSize: '13px',
              marginBottom: '16px',
            }}>
              {error}
            </div>
          )}
        </div>

        <div style={{
          display: 'flex',
          justifyContent: 'space-between',
          padding: '16px 20px',
          borderTop: '1px solid var(--border-color)',
        }}>
          <button
            className="btn btn-secondary"
            onClick={handleTest}
            disabled={testing || !formData.baseUrl || !formData.apiKey}
          >
            {testing ? (
              <><div className="spinner" style={{ width: '14px', height: '14px' }} /> Testing...</>
            ) : (
              <><Play size={14} /> Test</>
            )}
          </button>
          <div style={{ display: 'flex', gap: '8px' }}>
            <button className="btn btn-secondary" onClick={onClose}>
              Cancel
            </button>
            <button
              className="btn btn-primary"
              onClick={handleSave}
              disabled={saving || !formData.name || !formData.baseUrl || !formData.apiKey}
            >
              {saving ? 'Saving...' : 'Save'}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

// ============== DOWNLOAD CLIENTS SETTINGS ==============

function DownloadClientsSettings() {
  const [showModal, setShowModal] = useState(false);
  const [editingProvider, setEditingProvider] = useState<Provider | null>(null);
  const queryClient = useQueryClient();

  const { data: clients, isLoading, refetch } = useQuery({
    queryKey: ['downloadclients'],
    queryFn: api.getDownloadClients,
  });

  const toggleMutation = useMutation({
    mutationFn: ({ id, enabled }: { id: number; enabled: boolean }) => 
      api.setProviderEnabled(id, enabled),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['downloadclients'] }),
  });

  const deleteMutation = useMutation({
    mutationFn: api.deleteProvider,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['downloadclients'] }),
  });

  const handleAdd = () => {
    setEditingProvider(null);
    setShowModal(true);
  };

  const handleEdit = (provider: Provider) => {
    setEditingProvider(provider);
    setShowModal(true);
  };

  const handleDelete = (provider: Provider) => {
    if (confirm(`Delete download client "${provider.name}"? This cannot be undone.`)) {
      deleteMutation.mutate(provider.id);
    }
  };

  const handleModalClose = () => {
    setShowModal(false);
    setEditingProvider(null);
  };

  const handleModalSave = () => {
    queryClient.invalidateQueries({ queryKey: ['downloadclients'] });
    handleModalClose();
  };

  return (
    <>
      <SettingsSection title="Download Clients">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
          <p style={{ color: 'var(--text-muted)', fontSize: '13px', margin: 0 }}>
            Configure download clients for Usenet (SABnzbd), torrent, or DDL downloads.
          </p>
          <div style={{ display: 'flex', gap: '8px' }}>
            <button className="btn btn-icon" onClick={() => refetch()} title="Refresh">
              <RefreshCw size={16} />
            </button>
            <button className="btn btn-primary" onClick={handleAdd}>
              <Plus size={16} />
              Add Download Client
            </button>
          </div>
        </div>

        {isLoading ? (
          <div className="loading"><div className="spinner" /></div>
        ) : !clients?.length ? (
          <div className="empty-state" style={{ padding: '40px 20px' }}>
            <Download size={48} />
            <div className="empty-state-title">No download clients configured</div>
            <div className="empty-state-text">
              Add SABnzbd for Usenet downloads, or configure torrent/DDL clients.
            </div>
          </div>
        ) : (
          <ProviderTable
            providers={clients}
            onToggle={(id, enabled) => toggleMutation.mutate({ id, enabled })}
            onEdit={handleEdit}
            onDelete={handleDelete}
          />
        )}
      </SettingsSection>

      <SettingsSection title="DDL Download Settings">
        <div style={{ 
          padding: '12px 16px', 
          background: 'var(--bg-tertiary)', 
          borderRadius: 'var(--radius-md)',
          marginBottom: '16px',
          fontSize: '13px',
          color: 'var(--text-muted)'
        }}>
          <strong>Note:</strong> These settings only apply to Direct Download (DDL) sources like GetComics and ReadComicOnline. 
          SABnzbd, NZBGet, and torrent clients manage their own download queues and settings through their respective configuration panels.
        </div>
        
        <SettingsField 
          label="Maximum Concurrent DDL Downloads" 
          description="Number of direct HTTP downloads that can run simultaneously. Does not affect Usenet or torrent clients."
        >
          <input 
            className="input" 
            type="number"
            style={{ width: '100px' }}
            defaultValue={3}
            min={1}
            max={10}
          />
        </SettingsField>
        
        <SettingsField 
          label="DDL Download Timeout (seconds)" 
          description="Maximum time to wait for a direct HTTP download before failing. Does not affect Usenet or torrent clients."
        >
          <input 
            className="input" 
            type="number"
            style={{ width: '100px' }}
            defaultValue={300}
            min={30}
          />
        </SettingsField>
        
        <SettingsField 
          label="Retry Failed DDL Downloads"
          description="For DDL: Retries the HTTP download on network failures. Note: Usenet failures may require re-searching for a new release."
        >
          <label style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <input type="checkbox" defaultChecked />
            <span style={{ fontSize: '13px' }}>Automatically retry failed DDL downloads</span>
          </label>
        </SettingsField>
      </SettingsSection>

      {showModal && (
        <ProviderModal
          provider={editingProvider}
          category="DownloadClient"
          onClose={handleModalClose}
          onSave={handleModalSave}
        />
      )}
    </>
  );
}

// ============== SHARED PROVIDER COMPONENTS ==============

function ProviderTable({ 
  providers, 
  onToggle, 
  onEdit, 
  onDelete 
}: { 
  providers: Provider[];
  onToggle: (id: number, enabled: boolean) => void;
  onEdit: (provider: Provider) => void;
  onDelete: (provider: Provider) => void;
}) {
  const [testingId, setTestingId] = useState<number | null>(null);
  const [testResult, setTestResult] = useState<{ id: number; result: ProviderTestResult } | null>(null);

  const handleTest = async (provider: Provider) => {
    setTestingId(provider.id);
    setTestResult(null);
    try {
      const result = await api.testProvider(provider.id);
      setTestResult({ id: provider.id, result });
    } catch (e) {
      setTestResult({ 
        id: provider.id, 
        result: { success: false, message: 'Test failed', errors: [String(e)], latencyMs: 0 }
      });
    } finally {
      setTestingId(null);
    }
  };

  return (
    <div className="table-container">
      <table className="table">
        <thead>
          <tr>
            <th style={{ width: '40px' }}></th>
            <th>Name</th>
            <th>Type</th>
            <th>Status</th>
            <th>Priority</th>
            <th className="table-actions">Actions</th>
          </tr>
        </thead>
        <tbody>
          {providers.map((provider) => (
            <tr key={provider.id}>
              <td>
                <GripVertical size={16} style={{ color: 'var(--text-muted)', cursor: 'grab' }} />
              </td>
              <td>
                <div style={{ fontWeight: 500, color: 'var(--text-primary)' }}>{provider.name}</div>
                <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
                  {provider.baseUrl || provider.implementation}
                </div>
              </td>
              <td>
                <span className="badge badge-info">{provider.implementation}</span>
              </td>
              <td>
                <ProviderStatusBadge 
                  status={provider.lastHealthStatus} 
                  isEnabled={provider.isEnabled}
                  lastError={provider.lastError}
                />
                {testResult?.id === provider.id && (
                  <div style={{ marginTop: '4px' }}>
                    <span className={`badge badge-${testResult.result.success ? 'success' : 'danger'}`}>
                      {testResult.result.success ? 'Test passed' : 'Test failed'}
                    </span>
                    {testResult.result.latencyMs > 0 && (
                      <span style={{ marginLeft: '8px', fontSize: '12px', color: 'var(--text-muted)' }}>
                        {testResult.result.latencyMs}ms
                      </span>
                    )}
                  </div>
                )}
              </td>
              <td>{provider.priority}</td>
              <td className="table-actions">
                <button 
                  className="btn btn-icon" 
                  onClick={() => onToggle(provider.id, !provider.isEnabled)}
                  title={provider.isEnabled ? 'Disable' : 'Enable'}
                >
                  {provider.isEnabled ? <CheckCircle size={16} /> : <XCircle size={16} />}
                </button>
                <button 
                  className="btn btn-icon" 
                  onClick={() => handleTest(provider)}
                  disabled={testingId === provider.id}
                  title="Test"
                >
                  {testingId === provider.id ? (
                    <div className="spinner" style={{ width: '16px', height: '16px' }} />
                  ) : (
                    <Play size={16} />
                  )}
                </button>
                <button className="btn btn-icon" onClick={() => onEdit(provider)} title="Edit">
                  <Edit size={16} />
                </button>
                <button className="btn btn-icon" onClick={() => onDelete(provider)} title="Delete">
                  <Trash2 size={16} />
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function ProviderStatusBadge({ 
  status, 
  isEnabled,
  lastError 
}: { 
  status: string; 
  isEnabled: boolean;
  lastError: string | null;
}) {
  if (!isEnabled) {
    return <span className="badge badge-muted">Disabled</span>;
  }

  switch (status) {
    case 'Healthy':
      return <span className="badge badge-success">Healthy</span>;
    case 'Unhealthy':
      return (
        <span className="badge badge-danger" title={lastError || 'Unknown error'}>
          Unhealthy
        </span>
      );
    case 'Warning':
      return <span className="badge badge-warning">Warning</span>;
    default:
      return <span className="badge badge-muted">Unknown</span>;
  }
}

function ProviderModal({ 
  provider, 
  category,
  onClose, 
  onSave 
}: { 
  provider: Provider | null;
  category: 'Indexer' | 'DownloadClient';
  onClose: () => void;
  onSave: () => void;
}) {
  // Parse existing settings if editing
  const existingSettings = provider?.settings ? (() => {
    try { return JSON.parse(provider.settings); } catch { return {}; }
  })() : {};

  const [formData, setFormData] = useState<CreateProviderRequest>({
    name: provider?.name ?? '',
    implementation: provider?.implementation ?? '',
    isEnabled: provider?.isEnabled ?? true,
    baseUrl: provider?.baseUrl ?? '',
    apiKey: provider?.apiKey ?? '',
    username: provider?.username ?? '',
    password: '',
    settings: provider?.settings ?? '',
  });
  
  // SABnzbd-specific settings
  const [sabnzbdHost, setSabnzbdHost] = useState(existingSettings.host ?? '');
  const [sabnzbdPort, setSabnzbdPort] = useState<string>(existingSettings.port?.toString() ?? '');
  const [sabnzbdCategory, setSabnzbdCategory] = useState(existingSettings.category ?? 'comics');
  const [sabnzbdUseSsl, setSabnzbdUseSsl] = useState(existingSettings.useSsl ?? false);
  
  const [testing, setTesting] = useState(false);
  const [testResult, setTestResult] = useState<ProviderTestResult | null>(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Default port based on SSL setting
  const defaultPort = sabnzbdUseSsl ? 443 : 80;

  const { data: implementations } = useQuery({
    queryKey: ['provider-implementations'],
    queryFn: api.getProviderImplementations,
  });

  const filteredImplementations = implementations?.filter(
    i => i.category === category
  ) ?? [];

  const selectedImpl = filteredImplementations.find(i => i.name === formData.implementation);
  const isSabnzbd = formData.implementation === 'SABnzbd';
  
  // Build settings JSON for SABnzbd
  const getSettingsJson = () => {
    if (isSabnzbd) {
      const port = sabnzbdPort ? parseInt(sabnzbdPort, 10) : undefined;
      return JSON.stringify({
        host: sabnzbdHost,
        port: port && port > 0 && port <= 65535 ? port : undefined,
        apiKey: formData.apiKey,
        category: sabnzbdCategory,
        useSsl: sabnzbdUseSsl
      });
    }
    return formData.settings;
  };

  const handleTest = async () => {
    setTesting(true);
    setTestResult(null);
    setError(null);
    try {
      // Always test with current form data (not saved data)
      const requestData = { ...formData, settings: getSettingsJson() };
      const result = await api.testNewProvider(requestData);
      setTestResult(result);
      
      // If test is successful, automatically save
      if (result.success) {
        setSaving(true);
        try {
          if (provider) {
            await api.updateProvider(provider.id, requestData);
          } else if (category === 'Indexer') {
            await api.createIndexer(requestData);
          } else {
            await api.createDownloadClient(requestData);
          }
          // Update test result to show save success
          setTestResult({
            ...result,
            message: `${result.message}. Settings saved.`
          });
          // Close modal after short delay to show success message
          setTimeout(() => onSave(), 1000);
        } catch (saveError) {
          setError(`Test succeeded but save failed: ${String(saveError)}`);
        } finally {
          setSaving(false);
        }
      }
    } catch (e) {
      setTestResult({ success: false, message: 'Test failed', errors: [String(e)], latencyMs: 0 });
    } finally {
      setTesting(false);
    }
  };

  const handleSave = async () => {
    setSaving(true);
    setError(null);
    try {
      const requestData = { ...formData, settings: getSettingsJson() };
      if (provider) {
        await api.updateProvider(provider.id, requestData);
      } else if (category === 'Indexer') {
        await api.createIndexer(requestData);
      } else {
        await api.createDownloadClient(requestData);
      }
      onSave();
    } catch (e) {
      setError(String(e));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div style={{
      position: 'fixed',
      inset: 0,
      background: 'rgba(0, 0, 0, 0.7)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      zIndex: 1000,
    }}>
      <div style={{
        background: 'var(--bg-secondary)',
        borderRadius: 'var(--radius-lg)',
        border: '1px solid var(--border-color)',
        width: '100%',
        maxWidth: '500px',
        maxHeight: '90vh',
        overflow: 'auto',
      }}>
        <div style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          padding: '16px 20px',
          borderBottom: '1px solid var(--border-color)',
        }}>
          <h2 style={{ margin: 0, fontSize: '16px', fontWeight: 600 }}>
            {provider ? `Edit ${category}` : `Add ${category}`}
          </h2>
          <button className="btn btn-icon" onClick={onClose}>
            <X size={18} />
          </button>
        </div>

        <div style={{ padding: '20px' }}>
          <SettingsField label="Name">
            <input
              className="input"
              style={{ width: '100%' }}
              value={formData.name}
              onChange={(e) => setFormData({ ...formData, name: e.target.value })}
              placeholder={category === 'DownloadClient' ? 'My SABnzbd' : 'My RSS Feed'}
            />
          </SettingsField>

          <SettingsField label="Implementation">
            <select
              className="input"
              style={{ width: '100%' }}
              value={formData.implementation}
              onChange={(e) => setFormData({ ...formData, implementation: e.target.value })}
              disabled={!!provider}
            >
              <option value="">Select type...</option>
              {filteredImplementations.map((impl) => (
                <option key={impl.name} value={impl.name}>
                  {impl.displayName}
                </option>
              ))}
            </select>
            {selectedImpl?.description && (
              <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px' }}>
                {selectedImpl.description}
              </div>
            )}
          </SettingsField>

          {/* SABnzbd-specific Host/Port fields */}
          {isSabnzbd && (
            <>
              <SettingsField label="Host" description="SABnzbd hostname or IP address">
                <input
                  className="input"
                  style={{ width: '100%' }}
                  value={sabnzbdHost}
                  onChange={(e) => setSabnzbdHost(e.target.value)}
                  placeholder="localhost"
                />
              </SettingsField>

              <SettingsField label="Port" description={`Default: ${defaultPort} (${sabnzbdUseSsl ? 'HTTPS' : 'HTTP'})`}>
                <input
                  className="input"
                  style={{ width: '120px' }}
                  type="number"
                  min="1"
                  max="65535"
                  value={sabnzbdPort}
                  onChange={(e) => setSabnzbdPort(e.target.value)}
                  placeholder={String(defaultPort)}
                />
              </SettingsField>

              <SettingsField label="Use SSL">
                <label style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                  <input
                    type="checkbox"
                    checked={sabnzbdUseSsl}
                    onChange={(e) => setSabnzbdUseSsl(e.target.checked)}
                  />
                  <span style={{ fontSize: '13px' }}>Connect using HTTPS</span>
                </label>
              </SettingsField>
            </>
          )}

          {/* Base URL for non-SABnzbd providers */}
          {selectedImpl?.requiresBaseUrl && !isSabnzbd && (
            <SettingsField label="Base URL">
              <input
                className="input"
                style={{ width: '100%' }}
                value={formData.baseUrl}
                onChange={(e) => setFormData({ ...formData, baseUrl: e.target.value })}
                placeholder="https://example.com"
              />
            </SettingsField>
          )}

          {selectedImpl?.requiresApiKey && (
            <SettingsField label="API Key">
              <input
                className="input"
                style={{ width: '100%' }}
                type="password"
                value={formData.apiKey}
                onChange={(e) => setFormData({ ...formData, apiKey: e.target.value })}
                placeholder="Your API key"
              />
            </SettingsField>
          )}

          {/* SABnzbd-specific category setting */}
          {isSabnzbd && (
            <SettingsField label="Category" description="Category for comics downloads">
              <input
                className="input"
                style={{ width: '200px' }}
                value={sabnzbdCategory}
                onChange={(e) => setSabnzbdCategory(e.target.value)}
                placeholder="comics"
              />
            </SettingsField>
          )}

          {selectedImpl?.requiresCredentials && (
            <>
              <SettingsField label="Username">
                <input
                  className="input"
                  style={{ width: '100%' }}
                  value={formData.username}
                  onChange={(e) => setFormData({ ...formData, username: e.target.value })}
                />
              </SettingsField>
              <SettingsField label="Password">
                <input
                  className="input"
                  type="password"
                  style={{ width: '100%' }}
                  value={formData.password}
                  onChange={(e) => setFormData({ ...formData, password: e.target.value })}
                  placeholder={provider ? '(unchanged)' : ''}
                />
              </SettingsField>
            </>
          )}

          <SettingsField label="Enabled">
            <label style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <input
                type="checkbox"
                checked={formData.isEnabled}
                onChange={(e) => setFormData({ ...formData, isEnabled: e.target.checked })}
              />
              <span style={{ fontSize: '13px' }}>Enable this {category.toLowerCase()}</span>
            </label>
          </SettingsField>

          {testResult && (
            <div style={{
              padding: '12px',
              borderRadius: 'var(--radius-md)',
              background: testResult.success ? 'rgba(92, 184, 92, 0.1)' : 'rgba(217, 83, 79, 0.1)',
              border: `1px solid ${testResult.success ? 'var(--accent-success)' : 'var(--accent-danger)'}`,
              marginBottom: '16px',
            }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '4px' }}>
                {testResult.success ? (
                  <CheckCircle size={16} style={{ color: 'var(--accent-success)' }} />
                ) : (
                  <AlertCircle size={16} style={{ color: 'var(--accent-danger)' }} />
                )}
                <span style={{ fontWeight: 500, color: testResult.success ? 'var(--accent-success)' : 'var(--accent-danger)' }}>
                  {testResult.success ? 'Connection successful' : 'Connection failed'}
                </span>
              </div>
              <div style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>
                {testResult.message}
                {testResult.latencyMs > 0 && ` (${testResult.latencyMs}ms)`}
              </div>
              {testResult.errors?.length > 0 && (
                <ul style={{ margin: '8px 0 0 0', padding: '0 0 0 20px', fontSize: '12px', color: 'var(--accent-danger)' }}>
                  {testResult.errors.map((err, i) => (
                    <li key={i}>{err}</li>
                  ))}
                </ul>
              )}
            </div>
          )}

          {error && (
            <div style={{
              padding: '12px',
              borderRadius: 'var(--radius-md)',
              background: 'rgba(217, 83, 79, 0.1)',
              border: '1px solid var(--accent-danger)',
              color: 'var(--accent-danger)',
              fontSize: '13px',
              marginBottom: '16px',
            }}>
              {error}
            </div>
          )}
        </div>

        <div style={{
          display: 'flex',
          justifyContent: 'space-between',
          padding: '16px 20px',
          borderTop: '1px solid var(--border-color)',
        }}>
          <button 
            className="btn btn-secondary" 
            onClick={handleTest}
            disabled={testing || !formData.implementation}
          >
            {testing ? (
              <><div className="spinner" style={{ width: '14px', height: '14px' }} /> Testing...</>
            ) : (
              <><Play size={14} /> Test</>
            )}
          </button>
          <div style={{ display: 'flex', gap: '8px' }}>
            <button className="btn btn-secondary" onClick={onClose}>
              Cancel
            </button>
            <button 
              className="btn btn-primary" 
              onClick={handleSave}
              disabled={saving || !formData.name || !formData.implementation}
            >
              {saving ? 'Saving...' : 'Save'}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

// ============== OTHER SETTINGS ==============

function ImportSettings() {
  return (
    <>
      <SettingsSection title="Import Behavior">
        <SettingsField label="Auto-Import Matched Files">
          <label style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <input type="checkbox" defaultChecked />
            <span style={{ fontSize: '13px' }}>Automatically import files that match with high confidence</span>
          </label>
        </SettingsField>
        
        <SettingsField 
          label="Auto-Import Confidence Threshold" 
          description="Minimum match confidence for automatic import"
        >
          <input 
            className="input" 
            type="number"
            style={{ width: '100px' }}
            defaultValue={85}
            min={0}
            max={100}
          />
          <span style={{ marginLeft: '8px', color: 'var(--text-muted)' }}>%</span>
        </SettingsField>
        
        <SettingsField label="Copy vs Move">
          <select className="input" style={{ minWidth: '200px' }}>
            <option value="move">Move files (delete original)</option>
            <option value="copy">Copy files (keep original)</option>
          </select>
        </SettingsField>
      </SettingsSection>
      
      <SettingsSection title="Format Preferences">
        <SettingsField 
          label="Preferred Format" 
          description="When multiple formats are available"
        >
          <select className="input" style={{ minWidth: '150px' }}>
            <option value="cbz">CBZ</option>
            <option value="cbr">CBR</option>
            <option value="pdf">PDF</option>
          </select>
        </SettingsField>
        
        <SettingsField label="Convert to Preferred Format">
          <label style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <input type="checkbox" />
            <span style={{ fontSize: '13px' }}>Automatically convert files to preferred format on import</span>
          </label>
        </SettingsField>
      </SettingsSection>
    </>
  );
}

// === Notification Event Type Labels ===
const notificationEventLabels: Record<NotificationEventType, string> = {
  Test: 'Test Notification',
  NewRelease: 'New Release',
  Grabbed: 'Issue Grabbed',
  Imported: 'Issue Imported',
  WeeklySummary: 'Weekly Summary',
  DownloadFailed: 'Download Failed',
  SeriesAdded: 'Series Added',
  Health: 'Health Alert',
  Update: 'Application Update',
};

const allNotificationEvents: NotificationEventType[] = [
  'NewRelease',
  'Grabbed',
  'Imported',
  'WeeklySummary',
  'DownloadFailed',
  'SeriesAdded',
  'Health',
  'Update',
];

function NotificationsSettings() {
  const queryClient = useQueryClient();
  const [editingProvider, setEditingProvider] = useState<WebhookProviderSettings | null>(null);
  const [showAddModal, setShowAddModal] = useState(false);
  const [testingId, setTestingId] = useState<string | null>(null);
  const [testResult, setTestResult] = useState<{ success: boolean; message: string } | null>(null);

  // Fetch webhook providers
  const { data: providers = [], isLoading } = useQuery({
    queryKey: ['webhookProviders'],
    queryFn: api.getWebhookProviders,
  });

  // Add provider mutation
  const addMutation = useMutation({
    mutationFn: api.addWebhookProvider,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['webhookProviders'] });
      setShowAddModal(false);
    },
  });

  // Update provider mutation
  const updateMutation = useMutation({
    mutationFn: ({ id, provider }: { id: string; provider: WebhookProviderRequest }) =>
      api.updateWebhookProvider(id, provider),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['webhookProviders'] });
      setEditingProvider(null);
    },
  });

  // Delete provider mutation
  const deleteMutation = useMutation({
    mutationFn: api.deleteWebhookProvider,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['webhookProviders'] });
    },
  });

  // Test provider
  const handleTest = async (id: string) => {
    setTestingId(id);
    setTestResult(null);
    try {
      const result = await api.testWebhookProvider(id);
      setTestResult({ success: result.success, message: result.message });
    } catch (err) {
      setTestResult({ success: false, message: 'Failed to test webhook' });
    } finally {
      setTestingId(null);
    }
  };

  // Detect webhook type from URL
  const detectWebhookType = (url: string): string => {
    if (url.includes('discord.com/api/webhooks') || url.includes('discordapp.com/api/webhooks')) {
      return 'Discord';
    }
    if (url.includes('hooks.slack.com')) {
      return 'Slack';
    }
    return 'Generic';
  };

  return (
    <>
      <SettingsSection title="Webhook Providers">
        <div style={{ marginBottom: '16px' }}>
          <p style={{ fontSize: '13px', color: 'var(--text-muted)', marginBottom: '12px' }}>
            Configure webhook providers to receive notifications from Shortboxerr. Supports Discord, Slack, and generic webhooks.
          </p>
          <button
            className="btn btn-primary"
            onClick={() => setShowAddModal(true)}
            style={{ display: 'flex', alignItems: 'center', gap: '6px' }}
          >
            <Plus size={16} />
            Add Webhook
          </button>
        </div>

        {isLoading ? (
          <div style={{ padding: '20px', textAlign: 'center', color: 'var(--text-muted)' }}>
            Loading...
          </div>
        ) : providers.length === 0 ? (
          <div style={{ 
            padding: '32px', 
            textAlign: 'center', 
            color: 'var(--text-muted)',
            background: 'var(--bg-tertiary)',
            borderRadius: 'var(--radius-md)',
            border: '1px dashed var(--border-color)'
          }}>
            <Bell size={40} style={{ opacity: 0.3, marginBottom: '12px' }} />
            <p>No webhook providers configured</p>
            <p style={{ fontSize: '12px' }}>Add a webhook to start receiving notifications</p>
          </div>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
            {providers.map((provider) => (
              <div
                key={provider.id}
                style={{
                  padding: '16px',
                  background: 'var(--bg-tertiary)',
                  borderRadius: 'var(--radius-md)',
                  border: '1px solid var(--border-color)',
                }}
              >
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                  <div>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '4px' }}>
                      <span style={{ fontWeight: 600, fontSize: '14px' }}>{provider.name}</span>
                      <span style={{
                        fontSize: '11px',
                        padding: '2px 8px',
                        borderRadius: '10px',
                        background: provider.enabled ? 'rgba(34, 197, 94, 0.15)' : 'rgba(107, 114, 128, 0.15)',
                        color: provider.enabled ? 'rgb(34, 197, 94)' : 'var(--text-muted)',
                      }}>
                        {provider.enabled ? 'Enabled' : 'Disabled'}
                      </span>
                      <span style={{
                        fontSize: '11px',
                        padding: '2px 8px',
                        borderRadius: '10px',
                        background: 'rgba(59, 130, 246, 0.15)',
                        color: 'rgb(59, 130, 246)',
                      }}>
                        {detectWebhookType(provider.webhookUrl)}
                      </span>
                    </div>
                    <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginBottom: '8px' }}>
                      {provider.webhookUrl.substring(0, 60)}{provider.webhookUrl.length > 60 ? '...' : ''}
                    </div>
                    <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>
                      Events: {provider.onEvents?.map(e => notificationEventLabels[e]).join(', ') || 'None'}
                    </div>
                  </div>
                  <div style={{ display: 'flex', gap: '8px' }}>
                    <button
                      className="btn btn-sm"
                      onClick={() => handleTest(provider.id)}
                      disabled={testingId === provider.id}
                      title="Test webhook"
                      style={{ padding: '6px 10px' }}
                    >
                      {testingId === provider.id ? (
                        <RefreshCw size={14} className="spinning" />
                      ) : (
                        <Play size={14} />
                      )}
                    </button>
                    <button
                      className="btn btn-sm"
                      onClick={() => setEditingProvider(provider)}
                      title="Edit"
                      style={{ padding: '6px 10px' }}
                    >
                      <Edit size={14} />
                    </button>
                    <button
                      className="btn btn-sm btn-danger"
                      onClick={() => {
                        if (confirm(`Delete webhook "${provider.name}"?`)) {
                          deleteMutation.mutate(provider.id);
                        }
                      }}
                      title="Delete"
                      style={{ padding: '6px 10px' }}
                    >
                      <Trash2 size={14} />
                    </button>
                  </div>
                </div>
                {testResult && testingId === null && (
                  <div style={{
                    marginTop: '12px',
                    padding: '8px 12px',
                    borderRadius: 'var(--radius-sm)',
                    background: testResult.success ? 'rgba(34, 197, 94, 0.1)' : 'rgba(239, 68, 68, 0.1)',
                    color: testResult.success ? 'rgb(34, 197, 94)' : 'rgb(239, 68, 68)',
                    fontSize: '12px',
                    display: 'flex',
                    alignItems: 'center',
                    gap: '8px',
                  }}>
                    {testResult.success ? <CheckCircle size={14} /> : <XCircle size={14} />}
                    {testResult.message}
                  </div>
                )}
              </div>
            ))}
          </div>
        )}
      </SettingsSection>

      {/* Add/Edit Modal */}
      {(showAddModal || editingProvider) && (
        <WebhookProviderModal
          provider={editingProvider}
          onSave={(provider) => {
            if (editingProvider) {
              updateMutation.mutate({ id: editingProvider.id, provider });
            } else {
              addMutation.mutate(provider);
            }
          }}
          onClose={() => {
            setShowAddModal(false);
            setEditingProvider(null);
          }}
          isLoading={addMutation.isPending || updateMutation.isPending}
        />
      )}

      <EmailProvidersSection />
    </>
  );
}

function WebhookProviderModal({
  provider,
  onSave,
  onClose,
  isLoading,
}: {
  provider: WebhookProviderSettings | null;
  onSave: (provider: WebhookProviderRequest) => void;
  onClose: () => void;
  isLoading: boolean;
}) {
  const [name, setName] = useState(provider?.name || '');
  const [webhookUrl, setWebhookUrl] = useState(provider?.webhookUrl || '');
  const [enabled, setEnabled] = useState(provider?.enabled ?? true);
  const [onEvents, setOnEvents] = useState<NotificationEventType[]>(
    provider?.onEvents || ['Grabbed', 'NewRelease']
  );
  const [includeSeries, setIncludeSeries] = useState(provider?.includeSeries ?? true);
  const [includeImages, setIncludeImages] = useState(provider?.includeImages ?? true);
  const [showAdvanced, setShowAdvanced] = useState(false);
  const [username, setUsername] = useState(provider?.username || '');
  const [password, setPassword] = useState(provider?.password || '');
  const [testResult, setTestResult] = useState<{ success: boolean; message: string } | null>(null);
  const [testing, setTesting] = useState(false);

  const isValid = name.trim() && webhookUrl.trim();

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!isValid) return;
    onSave({
      name: name.trim(),
      webhookUrl: webhookUrl.trim(),
      enabled,
      onEvents,
      includeSeries,
      includeImages,
      username: username || undefined,
      password: password || undefined,
    });
  };

  const handleTest = async () => {
    if (!webhookUrl.trim()) return;
    setTesting(true);
    setTestResult(null);
    try {
      const result = await api.testWebhookProviderSettings({
        name: name || 'Test',
        webhookUrl: webhookUrl.trim(),
        enabled: true,
        onEvents,
        includeSeries,
        includeImages,
        username: username || undefined,
        password: password || undefined,
      });
      setTestResult({ success: result.success, message: result.message });
    } catch (err) {
      setTestResult({ success: false, message: 'Failed to test webhook' });
    } finally {
      setTesting(false);
    }
  };

  const toggleEvent = (event: NotificationEventType) => {
    setOnEvents(prev =>
      prev.includes(event) ? prev.filter(e => e !== event) : [...prev, event]
    );
  };

  // Detect webhook type
  const webhookType = webhookUrl ? (
    webhookUrl.includes('discord.com/api/webhooks') || webhookUrl.includes('discordapp.com/api/webhooks')
      ? 'Discord'
      : webhookUrl.includes('hooks.slack.com')
        ? 'Slack'
        : 'Generic'
  ) : '';

  return (
    <div style={{
      position: 'fixed',
      top: 0,
      left: 0,
      right: 0,
      bottom: 0,
      background: 'rgba(0, 0, 0, 0.5)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      zIndex: 1000,
    }}>
      <div style={{
        background: 'var(--bg-secondary)',
        borderRadius: 'var(--radius-lg)',
        border: '1px solid var(--border-color)',
        maxWidth: '600px',
        width: '90%',
        maxHeight: '90vh',
        overflow: 'auto',
      }}>
        <div style={{
          padding: '16px 20px',
          borderBottom: '1px solid var(--border-color)',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
        }}>
          <h3 style={{ margin: 0, fontSize: '16px', fontWeight: 600 }}>
            {provider ? 'Edit Webhook' : 'Add Webhook'}
          </h3>
          <button
            className="btn btn-sm"
            onClick={onClose}
            style={{ padding: '4px 8px', background: 'transparent' }}
          >
            <X size={18} />
          </button>
        </div>

        <form onSubmit={handleSubmit} style={{ padding: '20px' }}>
          <SettingsField label="Name" description="A friendly name to identify this webhook">
            <input
              className="input"
              type="text"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="My Discord Webhook"
              style={{ width: '100%' }}
            />
          </SettingsField>

          <SettingsField label="Webhook URL" description="Discord, Slack, or any HTTP webhook URL">
            <input
              className="input"
              type="url"
              value={webhookUrl}
              onChange={(e) => setWebhookUrl(e.target.value)}
              placeholder="https://discord.com/api/webhooks/..."
              style={{ width: '100%' }}
            />
            {webhookType && (
              <div style={{ marginTop: '8px' }}>
                <span style={{
                  fontSize: '11px',
                  padding: '2px 8px',
                  borderRadius: '10px',
                  background: 'rgba(59, 130, 246, 0.15)',
                  color: 'rgb(59, 130, 246)',
                }}>
                  Detected: {webhookType}
                </span>
              </div>
            )}
          </SettingsField>

          <SettingsField label="Enabled">
            <label style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <input
                type="checkbox"
                checked={enabled}
                onChange={(e) => setEnabled(e.target.checked)}
              />
              <span style={{ fontSize: '13px' }}>Enable this webhook</span>
            </label>
          </SettingsField>

          <SettingsField label="Notification Events" description="Select which events should trigger this webhook">
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: '8px' }}>
              {allNotificationEvents.map((event) => (
                <label
                  key={event}
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: '6px',
                    padding: '6px 12px',
                    borderRadius: 'var(--radius-sm)',
                    background: onEvents.includes(event) ? 'rgba(59, 130, 246, 0.15)' : 'var(--bg-tertiary)',
                    border: `1px solid ${onEvents.includes(event) ? 'rgb(59, 130, 246)' : 'var(--border-color)'}`,
                    cursor: 'pointer',
                    fontSize: '12px',
                  }}
                >
                  <input
                    type="checkbox"
                    checked={onEvents.includes(event)}
                    onChange={() => toggleEvent(event)}
                    style={{ display: 'none' }}
                  />
                  {notificationEventLabels[event]}
                </label>
              ))}
            </div>
          </SettingsField>

          <SettingsField label="Payload Options">
            <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
              <label style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <input
                  type="checkbox"
                  checked={includeSeries}
                  onChange={(e) => setIncludeSeries(e.target.checked)}
                />
                <span style={{ fontSize: '13px' }}>Include series information</span>
              </label>
              <label style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <input
                  type="checkbox"
                  checked={includeImages}
                  onChange={(e) => setIncludeImages(e.target.checked)}
                />
                <span style={{ fontSize: '13px' }}>Include cover images (Discord/Slack)</span>
              </label>
            </div>
          </SettingsField>

          <div style={{ marginBottom: '16px' }}>
            <button
              type="button"
              className="btn btn-sm"
              onClick={() => setShowAdvanced(!showAdvanced)}
              style={{ fontSize: '12px', padding: '4px 10px' }}
            >
              {showAdvanced ? 'Hide' : 'Show'} Advanced Options
            </button>
          </div>

          {showAdvanced && (
            <>
              <SettingsField label="Basic Auth Username (optional)">
                <input
                  className="input"
                  type="text"
                  value={username}
                  onChange={(e) => setUsername(e.target.value)}
                  placeholder="username"
                  style={{ width: '100%' }}
                />
              </SettingsField>
              <SettingsField label="Basic Auth Password (optional)">
                <input
                  className="input"
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="password"
                  style={{ width: '100%' }}
                />
              </SettingsField>
            </>
          )}

          {testResult && (
            <div style={{
              marginBottom: '16px',
              padding: '12px',
              borderRadius: 'var(--radius-sm)',
              background: testResult.success ? 'rgba(34, 197, 94, 0.1)' : 'rgba(239, 68, 68, 0.1)',
              color: testResult.success ? 'rgb(34, 197, 94)' : 'rgb(239, 68, 68)',
              fontSize: '13px',
              display: 'flex',
              alignItems: 'center',
              gap: '8px',
            }}>
              {testResult.success ? <CheckCircle size={16} /> : <XCircle size={16} />}
              {testResult.message}
            </div>
          )}

          <div style={{ display: 'flex', gap: '12px', justifyContent: 'flex-end' }}>
            <button
              type="button"
              className="btn"
              onClick={handleTest}
              disabled={!webhookUrl.trim() || testing}
            >
              {testing ? (
                <>
                  <RefreshCw size={14} className="spinning" />
                  Testing...
                </>
              ) : (
                <>
                  <Play size={14} />
                  Test
                </>
              )}
            </button>
            <button
              type="button"
              className="btn"
              onClick={onClose}
            >
              Cancel
            </button>
            <button
              type="submit"
              className="btn btn-primary"
              disabled={!isValid || isLoading}
            >
              {isLoading ? 'Saving...' : (provider ? 'Save Changes' : 'Add Webhook')}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

function EmailProvidersSection() {
  const queryClient = useQueryClient();
  const [editingProvider, setEditingProvider] = useState<EmailProviderSettings | null>(null);
  const [showAddModal, setShowAddModal] = useState(false);
  const [testingId, setTestingId] = useState<string | null>(null);
  const [testResult, setTestResult] = useState<{ success: boolean; message: string } | null>(null);

  // Fetch email providers
  const { data: providers = [], isLoading } = useQuery({
    queryKey: ['emailProviders'],
    queryFn: api.getEmailProviders,
  });

  // Add provider mutation
  const addMutation = useMutation({
    mutationFn: api.addEmailProvider,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['emailProviders'] });
      setShowAddModal(false);
    },
  });

  // Update provider mutation
  const updateMutation = useMutation({
    mutationFn: ({ id, provider }: { id: string; provider: EmailProviderRequest }) =>
      api.updateEmailProvider(id, provider),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['emailProviders'] });
      setEditingProvider(null);
    },
  });

  // Delete provider mutation
  const deleteMutation = useMutation({
    mutationFn: api.deleteEmailProvider,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['emailProviders'] });
    },
  });

  // Test provider
  const handleTest = async (id: string) => {
    setTestingId(id);
    setTestResult(null);
    try {
      const result = await api.testEmailProvider(id);
      setTestResult({ success: result.success, message: result.message });
    } catch (err) {
      setTestResult({ success: false, message: 'Failed to test email provider' });
    } finally {
      setTestingId(null);
    }
  };

  return (
    <>
      <SettingsSection title="Email Providers">
        <div style={{ marginBottom: '16px' }}>
          <p style={{ fontSize: '13px', color: 'var(--text-muted)', marginBottom: '12px' }}>
            Configure SMTP email providers to receive notifications from Shortboxerr.
          </p>
          <button
            className="btn btn-primary"
            onClick={() => setShowAddModal(true)}
            style={{ display: 'flex', alignItems: 'center', gap: '6px' }}
          >
            <Plus size={16} />
            Add Email Provider
          </button>
        </div>

        {isLoading ? (
          <div style={{ padding: '20px', textAlign: 'center', color: 'var(--text-muted)' }}>
            Loading...
          </div>
        ) : providers.length === 0 ? (
          <div style={{ 
            padding: '32px', 
            textAlign: 'center', 
            color: 'var(--text-muted)',
            background: 'var(--bg-tertiary)',
            borderRadius: 'var(--radius-md)',
            border: '1px dashed var(--border-color)'
          }}>
            <Bell size={40} style={{ opacity: 0.3, marginBottom: '12px' }} />
            <p>No email providers configured</p>
            <p style={{ fontSize: '12px' }}>Add an email provider to receive notifications via SMTP</p>
          </div>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
            {providers.map((provider) => (
              <div
                key={provider.id}
                style={{
                  padding: '16px',
                  background: 'var(--bg-tertiary)',
                  borderRadius: 'var(--radius-md)',
                  border: '1px solid var(--border-color)',
                }}
              >
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                  <div>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '4px' }}>
                      <span style={{ fontWeight: 600, fontSize: '14px' }}>{provider.name}</span>
                      <span style={{
                        fontSize: '11px',
                        padding: '2px 8px',
                        borderRadius: '10px',
                        background: provider.enabled ? 'rgba(34, 197, 94, 0.15)' : 'rgba(107, 114, 128, 0.15)',
                        color: provider.enabled ? 'rgb(34, 197, 94)' : 'var(--text-muted)',
                      }}>
                        {provider.enabled ? 'Enabled' : 'Disabled'}
                      </span>
                      <span style={{
                        fontSize: '11px',
                        padding: '2px 8px',
                        borderRadius: '10px',
                        background: 'rgba(59, 130, 246, 0.15)',
                        color: 'rgb(59, 130, 246)',
                      }}>
                        {provider.smtpServer}:{provider.port}
                      </span>
                    </div>
                    <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginBottom: '8px' }}>
                      To: {provider.recipientEmails}
                    </div>
                    <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>
                      Events: {provider.onEvents?.map(e => notificationEventLabels[e]).join(', ') || 'None'}
                    </div>
                  </div>
                  <div style={{ display: 'flex', gap: '8px' }}>
                    <button
                      className="btn btn-sm"
                      onClick={() => handleTest(provider.id)}
                      disabled={testingId === provider.id}
                      title="Test email"
                      style={{ padding: '6px 10px' }}
                    >
                      {testingId === provider.id ? (
                        <RefreshCw size={14} className="spinning" />
                      ) : (
                        <Play size={14} />
                      )}
                    </button>
                    <button
                      className="btn btn-sm"
                      onClick={() => setEditingProvider(provider)}
                      title="Edit"
                      style={{ padding: '6px 10px' }}
                    >
                      <Edit size={14} />
                    </button>
                    <button
                      className="btn btn-sm btn-danger"
                      onClick={() => {
                        if (confirm(`Delete email provider "${provider.name}"?`)) {
                          deleteMutation.mutate(provider.id);
                        }
                      }}
                      title="Delete"
                      style={{ padding: '6px 10px' }}
                    >
                      <Trash2 size={14} />
                    </button>
                  </div>
                </div>
                {testResult && testingId === null && (
                  <div style={{
                    marginTop: '12px',
                    padding: '8px 12px',
                    borderRadius: 'var(--radius-sm)',
                    background: testResult.success ? 'rgba(34, 197, 94, 0.1)' : 'rgba(239, 68, 68, 0.1)',
                    color: testResult.success ? 'rgb(34, 197, 94)' : 'rgb(239, 68, 68)',
                    fontSize: '12px',
                    display: 'flex',
                    alignItems: 'center',
                    gap: '8px',
                  }}>
                    {testResult.success ? <CheckCircle size={14} /> : <XCircle size={14} />}
                    {testResult.message}
                  </div>
                )}
              </div>
            ))}
          </div>
        )}
      </SettingsSection>

      {/* Add/Edit Email Modal */}
      {(showAddModal || editingProvider) && (
        <EmailProviderModal
          provider={editingProvider}
          onSave={(provider) => {
            if (editingProvider) {
              updateMutation.mutate({ id: editingProvider.id, provider });
            } else {
              addMutation.mutate(provider);
            }
          }}
          onClose={() => {
            setShowAddModal(false);
            setEditingProvider(null);
          }}
          isLoading={addMutation.isPending || updateMutation.isPending}
        />
      )}
    </>
  );
}

function EmailProviderModal({
  provider,
  onSave,
  onClose,
  isLoading,
}: {
  provider: EmailProviderSettings | null;
  onSave: (provider: EmailProviderRequest) => void;
  onClose: () => void;
  isLoading: boolean;
}) {
  const [name, setName] = useState(provider?.name || '');
  const [smtpServer, setSmtpServer] = useState(provider?.smtpServer || '');
  const [port, setPort] = useState(provider?.port ?? 587);
  const [useSsl, setUseSsl] = useState(provider?.useSsl ?? true);
  const [username, setUsername] = useState(provider?.username || '');
  const [password, setPassword] = useState(provider?.password || '');
  const [senderEmail, setSenderEmail] = useState(provider?.senderEmail || '');
  const [senderName, setSenderName] = useState(provider?.senderName || 'Shortboxerr');
  const [recipientEmails, setRecipientEmails] = useState(provider?.recipientEmails || '');
  const [ccEmails, setCcEmails] = useState(provider?.ccEmails || '');
  const [bccEmails, setBccEmails] = useState(provider?.bccEmails || '');
  const [subjectPrefix, setSubjectPrefix] = useState(provider?.subjectPrefix || '[Shortboxerr]');
  const [useHtml, setUseHtml] = useState(provider?.useHtml ?? true);
  const [enabled, setEnabled] = useState(provider?.enabled ?? true);
  const [onEvents, setOnEvents] = useState<NotificationEventType[]>(
    provider?.onEvents || ['Grabbed', 'NewRelease']
  );
  const [includeSeries, setIncludeSeries] = useState(provider?.includeSeries ?? true);
  const [includeImages, setIncludeImages] = useState(provider?.includeImages ?? false);
  const [showAdvanced, setShowAdvanced] = useState(false);
  const [testResult, setTestResult] = useState<{ success: boolean; message: string } | null>(null);
  const [testing, setTesting] = useState(false);

  const isValid = name.trim() && smtpServer.trim() && senderEmail.trim() && recipientEmails.trim();

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!isValid) return;
    onSave({
      name: name.trim(),
      smtpServer: smtpServer.trim(),
      port,
      useSsl,
      username: username || undefined,
      password: password || undefined,
      senderEmail: senderEmail.trim(),
      senderName: senderName || undefined,
      recipientEmails: recipientEmails.trim(),
      ccEmails: ccEmails || undefined,
      bccEmails: bccEmails || undefined,
      subjectPrefix,
      useHtml,
      enabled,
      onEvents,
      includeSeries,
      includeImages,
    });
  };

  const handleTest = async () => {
    if (!smtpServer.trim() || !senderEmail.trim() || !recipientEmails.trim()) return;
    setTesting(true);
    setTestResult(null);
    try {
      const result = await api.testEmailProviderSettings({
        name: name || 'Test',
        smtpServer: smtpServer.trim(),
        port,
        useSsl,
        username: username || undefined,
        password: password || undefined,
        senderEmail: senderEmail.trim(),
        senderName: senderName || undefined,
        recipientEmails: recipientEmails.trim(),
        ccEmails: ccEmails || undefined,
        bccEmails: bccEmails || undefined,
        subjectPrefix,
        useHtml,
        enabled: true,
        onEvents,
        includeSeries,
        includeImages,
      });
      setTestResult({ success: result.success, message: result.message });
    } catch (err) {
      setTestResult({ success: false, message: 'Failed to test email provider' });
    } finally {
      setTesting(false);
    }
  };

  const toggleEvent = (event: NotificationEventType) => {
    setOnEvents(prev =>
      prev.includes(event) ? prev.filter(e => e !== event) : [...prev, event]
    );
  };

  return (
    <div style={{
      position: 'fixed',
      inset: 0,
      background: 'rgba(0, 0, 0, 0.6)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      zIndex: 1000,
    }}>
      <div style={{
        background: 'var(--bg-secondary)',
        borderRadius: 'var(--radius-lg)',
        width: '100%',
        maxWidth: '550px',
        maxHeight: '90vh',
        overflow: 'auto',
        border: '1px solid var(--border-color)',
      }}>
        <div style={{
          padding: '16px 20px',
          borderBottom: '1px solid var(--border-color)',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
        }}>
          <h3 style={{ margin: 0, fontSize: '16px', fontWeight: 600 }}>
            {provider ? 'Edit Email Provider' : 'Add Email Provider'}
          </h3>
          <button
            className="btn btn-sm"
            onClick={onClose}
            style={{ padding: '4px 8px', background: 'transparent' }}
          >
            <X size={18} />
          </button>
        </div>

        <form onSubmit={handleSubmit} style={{ padding: '20px' }}>
          <SettingsField label="Name" description="A friendly name to identify this email provider">
            <input
              className="input"
              type="text"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="My Email Provider"
              style={{ width: '100%' }}
            />
          </SettingsField>

          <SettingsField label="SMTP Server" description="SMTP server hostname (e.g., smtp.gmail.com)">
            <input
              className="input"
              type="text"
              value={smtpServer}
              onChange={(e) => setSmtpServer(e.target.value)}
              placeholder="smtp.gmail.com"
              style={{ width: '100%' }}
            />
          </SettingsField>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px' }}>
            <SettingsField label="Port" description="SMTP port">
              <input
                className="input"
                type="number"
                value={port}
                onChange={(e) => setPort(parseInt(e.target.value) || 587)}
                min={1}
                max={65535}
                style={{ width: '100%' }}
              />
            </SettingsField>

            <SettingsField label="SSL/TLS">
              <label style={{ display: 'flex', alignItems: 'center', gap: '8px', marginTop: '8px' }}>
                <input
                  type="checkbox"
                  checked={useSsl}
                  onChange={(e) => setUseSsl(e.target.checked)}
                />
                <span style={{ fontSize: '13px' }}>Use SSL/TLS encryption</span>
              </label>
            </SettingsField>
          </div>

          <SettingsField label="Username" description="SMTP authentication username (optional)">
            <input
              className="input"
              type="text"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              placeholder="user@example.com"
              style={{ width: '100%' }}
            />
          </SettingsField>

          <SettingsField label="Password" description="SMTP authentication password (optional)">
            <input
              className="input"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="••••••••"
              style={{ width: '100%' }}
            />
          </SettingsField>

          <SettingsField label="Sender Email" description="From address for emails">
            <input
              className="input"
              type="email"
              value={senderEmail}
              onChange={(e) => setSenderEmail(e.target.value)}
              placeholder="notifications@example.com"
              style={{ width: '100%' }}
            />
          </SettingsField>

          <SettingsField label="Sender Name" description="Display name for sender (optional)">
            <input
              className="input"
              type="text"
              value={senderName}
              onChange={(e) => setSenderName(e.target.value)}
              placeholder="Shortboxerr"
              style={{ width: '100%' }}
            />
          </SettingsField>

          <SettingsField label="Recipients" description="Email addresses to send notifications to (comma-separated)">
            <input
              className="input"
              type="text"
              value={recipientEmails}
              onChange={(e) => setRecipientEmails(e.target.value)}
              placeholder="user@example.com, another@example.com"
              style={{ width: '100%' }}
            />
          </SettingsField>

          <SettingsField label="">
            <label style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <input
                type="checkbox"
                checked={enabled}
                onChange={(e) => setEnabled(e.target.checked)}
              />
              <span style={{ fontSize: '13px' }}>Enable this email provider</span>
            </label>
          </SettingsField>

          <SettingsField label="Notification Events" description="Select which events should trigger emails">
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: '8px' }}>
              {allNotificationEvents.map((event) => (
                <label
                  key={event}
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: '6px',
                    padding: '6px 12px',
                    background: onEvents.includes(event) ? 'var(--accent-color)' : 'var(--bg-tertiary)',
                    color: onEvents.includes(event) ? 'white' : 'var(--text-color)',
                    borderRadius: 'var(--radius-sm)',
                    cursor: 'pointer',
                    fontSize: '12px',
                    transition: 'all 0.15s ease',
                  }}
                >
                  <input
                    type="checkbox"
                    checked={onEvents.includes(event)}
                    onChange={() => toggleEvent(event)}
                    style={{ display: 'none' }}
                  />
                  {notificationEventLabels[event]}
                </label>
              ))}
            </div>
          </SettingsField>

          <button
            type="button"
            onClick={() => setShowAdvanced(!showAdvanced)}
            style={{
              background: 'none',
              border: 'none',
              color: 'var(--accent-color)',
              cursor: 'pointer',
              fontSize: '13px',
              padding: '8px 0',
              marginBottom: '12px',
            }}
          >
            {showAdvanced ? '▼' : '►'} Advanced Options
          </button>

          {showAdvanced && (
            <div style={{ 
              padding: '16px', 
              background: 'var(--bg-tertiary)', 
              borderRadius: 'var(--radius-md)',
              marginBottom: '16px'
            }}>
              <SettingsField label="CC" description="CC recipients (comma-separated)">
                <input
                  className="input"
                  type="text"
                  value={ccEmails}
                  onChange={(e) => setCcEmails(e.target.value)}
                  placeholder="cc@example.com"
                  style={{ width: '100%' }}
                />
              </SettingsField>

              <SettingsField label="BCC" description="BCC recipients (comma-separated)">
                <input
                  className="input"
                  type="text"
                  value={bccEmails}
                  onChange={(e) => setBccEmails(e.target.value)}
                  placeholder="bcc@example.com"
                  style={{ width: '100%' }}
                />
              </SettingsField>

              <SettingsField label="Subject Prefix" description="Prefix for email subject lines">
                <input
                  className="input"
                  type="text"
                  value={subjectPrefix}
                  onChange={(e) => setSubjectPrefix(e.target.value)}
                  placeholder="[Shortboxerr]"
                  style={{ width: '100%' }}
                />
              </SettingsField>

              <SettingsField label="Email Format">
                <label style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                  <input
                    type="checkbox"
                    checked={useHtml}
                    onChange={(e) => setUseHtml(e.target.checked)}
                  />
                  <span style={{ fontSize: '13px' }}>Use HTML formatting</span>
                </label>
              </SettingsField>

              <SettingsField label="Content Options">
                <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                  <label style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                    <input
                      type="checkbox"
                      checked={includeSeries}
                      onChange={(e) => setIncludeSeries(e.target.checked)}
                    />
                    <span style={{ fontSize: '13px' }}>Include series information</span>
                  </label>
                  <label style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                    <input
                      type="checkbox"
                      checked={includeImages}
                      onChange={(e) => setIncludeImages(e.target.checked)}
                    />
                    <span style={{ fontSize: '13px' }}>Include cover images (as attachments)</span>
                  </label>
                </div>
              </SettingsField>
            </div>
          )}

          {testResult && (
            <div style={{
              marginBottom: '16px',
              padding: '12px',
              borderRadius: 'var(--radius-sm)',
              background: testResult.success ? 'rgba(34, 197, 94, 0.1)' : 'rgba(239, 68, 68, 0.1)',
              color: testResult.success ? 'rgb(34, 197, 94)' : 'rgb(239, 68, 68)',
              fontSize: '13px',
              display: 'flex',
              alignItems: 'center',
              gap: '8px',
            }}>
              {testResult.success ? <CheckCircle size={16} /> : <XCircle size={16} />}
              {testResult.message}
            </div>
          )}

          <div style={{ display: 'flex', gap: '12px', justifyContent: 'flex-end' }}>
            <button
              type="button"
              className="btn"
              onClick={handleTest}
              disabled={!smtpServer.trim() || !senderEmail.trim() || !recipientEmails.trim() || testing}
            >
              {testing ? (
                <>
                  <RefreshCw size={14} className="spinning" />
                  Testing...
                </>
              ) : (
                <>
                  <Play size={14} />
                  Test
                </>
              )}
            </button>
            <button
              type="button"
              className="btn"
              onClick={onClose}
            >
              Cancel
            </button>
            <button
              type="submit"
              className="btn btn-primary"
              disabled={!isValid || isLoading}
            >
              {isLoading ? 'Saving...' : (provider ? 'Save Changes' : 'Add Email Provider')}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

function UISettings() {
  const { theme, setTheme } = useTheme();

  return (
    <SettingsSection title="UI Preferences">
      <SettingsField label="Theme">
        <select 
          className="input" 
          style={{ minWidth: '150px' }}
          value={theme}
          onChange={(e) => setTheme(e.target.value as 'dark' | 'light' | 'system')}
        >
          <option value="dark">Dark</option>
          <option value="light">Light</option>
          <option value="system">System</option>
        </select>
      </SettingsField>
      
      <SettingsField 
        label="Table Page Size" 
        description="Number of items to show per page"
      >
        <select className="input" style={{ minWidth: '100px' }}>
          <option value="25">25</option>
          <option value="50">50</option>
          <option value="100">100</option>
        </select>
      </SettingsField>
      
      <SettingsField label="Show File Sizes">
        <label style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
          <input type="checkbox" defaultChecked />
          <span style={{ fontSize: '13px' }}>Display file sizes in tables</span>
        </label>
      </SettingsField>
    </SettingsSection>
  );
}

function SecuritySettings() {
  return (
    <SettingsSection title="Security">
      <SettingsField label="Authentication" description="Authentication method for accessing the UI">
        <select className="input" style={{ width: '200px' }}>
          <option value="none">None</option>
          <option value="basic">Basic (Browser popup)</option>
          <option value="forms">Forms (Login page)</option>
        </select>
      </SettingsField>
    </SettingsSection>
  );
}
