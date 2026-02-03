import { useState, useRef, useEffect } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { 
  Settings, Server, Download, Shield, 
  FolderOpen, Plug, Save, Plus, Edit, Trash2, 
  CheckCircle, XCircle, AlertCircle, Play, GripVertical,
  Copy, RefreshCw, X, Database, ExternalLink
} from 'lucide-react';
import { api } from '../api/client';
import type { Provider, CreateProviderRequest, ProviderTestResult, ComicVineTestResult } from '../api/client';
import { useTheme } from '../App';

type SettingsTab = 'general' | 'indexers' | 'download' | 'import' | 'ui' | 'security' | 'comicvine';

const tabs: { id: SettingsTab; icon: React.ElementType; label: string }[] = [
  { id: 'general', icon: Settings, label: 'General' },
  { id: 'comicvine', icon: Database, label: 'ComicVine' },
  { id: 'indexers', icon: Plug, label: 'Indexers' },
  { id: 'download', icon: Download, label: 'Download Clients' },
  { id: 'import', icon: FolderOpen, label: 'Import' },
  { id: 'ui', icon: Server, label: 'UI' },
  { id: 'security', icon: Shield, label: 'Security' },
];

export function SettingsPage() {
  const [activeTab, setActiveTab] = useState<SettingsTab>('general');

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
                onClick={() => setActiveTab(tab.id)}
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
            {activeTab === 'indexers' && <IndexersSettings />}
            {activeTab === 'download' && <DownloadClientsSettings />}
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

// ============== COMICVINE SETTINGS ==============

function ComicVineSettingsTab() {
  const [apiKey, setApiKey] = useState('');
  const [showApiKey, setShowApiKey] = useState(false);
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
    enabled: settings?.enabled ?? false,
  });

  const updateMutation = useMutation({
    mutationFn: api.updateComicVineSettings,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['comicvineSettings'] });
    },
  });

  const handleTestConnection = async () => {
    setIsTesting(true);
    setTestResult(null);
    try {
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

  const handleToggleEnabled = (enabled: boolean) => {
    updateMutation.mutate({ enabled });
  };

  if (isLoading) {
    return <div className="loading"><div className="spinner" /></div>;
  }

  return (
    <>
      <SettingsSection title="ComicVine API">
        <div style={{ 
          padding: '12px 16px', 
          background: 'var(--bg-tertiary)', 
          borderRadius: 'var(--radius-md)',
          marginBottom: '16px',
          fontSize: '13px',
          color: 'var(--text-secondary)'
        }}>
          <p style={{ margin: 0 }}>
            ComicVine provides metadata for comic series, issues, and collections.
            Get your API key from{' '}
            <a 
              href="https://comicvine.gamespot.com/api/" 
              target="_blank" 
              rel="noopener noreferrer"
              style={{ color: 'var(--accent-primary)', textDecoration: 'none' }}
            >
              comicvine.gamespot.com/api <ExternalLink size={12} style={{ verticalAlign: 'middle' }} />
            </a>
          </p>
        </div>

        <SettingsField label="Enable ComicVine" description="Use ComicVine for metadata lookup">
          <label style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer' }}>
            <input
              type="checkbox"
              checked={settings?.enabled ?? false}
              onChange={(e) => handleToggleEnabled(e.target.checked)}
              style={{ width: '18px', height: '18px', cursor: 'pointer' }}
            />
            <span style={{ color: 'var(--text-secondary)', fontSize: '13px' }}>
              {settings?.enabled ? 'Enabled' : 'Disabled'}
            </span>
          </label>
        </SettingsField>

        <SettingsField 
          label="API Key" 
          description={settings?.hasApiKey ? `Current key: ${settings.maskedApiKey}` : 'No API key configured'}
        >
          <div style={{ display: 'flex', gap: '8px' }}>
            <input
              className="input"
              type={showApiKey ? 'text' : 'password'}
              value={apiKey}
              onChange={(e) => setApiKey(e.target.value)}
              placeholder={settings?.hasApiKey ? 'Enter new key to replace' : 'Enter your API key'}
              style={{ flex: 1 }}
            />
            <button
              className="btn btn-icon"
              onClick={() => setShowApiKey(!showApiKey)}
              title={showApiKey ? 'Hide' : 'Show'}
            >
              {showApiKey ? <XCircle size={16} /> : <CheckCircle size={16} />}
            </button>
            <button
              className="btn btn-primary"
              onClick={handleSaveApiKey}
              disabled={!apiKey.trim() || updateMutation.isPending}
            >
              <Save size={16} />
              Save Key
            </button>
          </div>
        </SettingsField>

        <div style={{ display: 'flex', gap: '12px', alignItems: 'center', marginTop: '16px' }}>
          <button
            className="btn btn-secondary"
            onClick={handleTestConnection}
            disabled={!settings?.hasApiKey || isTesting}
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
      </SettingsSection>

      {settings?.enabled && (
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

// ============== INDEXERS SETTINGS ==============

function IndexersSettings() {
  const [showModal, setShowModal] = useState(false);
  const [editingProvider, setEditingProvider] = useState<Provider | null>(null);
  const queryClient = useQueryClient();

  // Only fetch RSS indexers (DDL indexers are built-in)
  const { data: indexers, isLoading, refetch } = useQuery({
    queryKey: ['indexers'],
    queryFn: api.getIndexers,
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

  return (
    <>
      <SettingsSection title="DDL Sites (Built-in)">
        <p style={{ color: 'var(--text-muted)', fontSize: '13px', marginBottom: '16px' }}>
          DDL indexers (GetComics, etc.) are built-in with Mylar3 parity. Configure site-specific settings below.
        </p>
        <div style={{ 
          display: 'grid', 
          gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))', 
          gap: '12px' 
        }}>
          <DdlSiteCard 
            name="GetComics.org" 
            description="Primary DDL source for comics"
            enabled={true}
          />
          <DdlSiteCard 
            name="ReadComicOnline" 
            description="Online comic reader with downloads"
            enabled={false}
          />
        </div>
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
    </>
  );
}

function DdlSiteCard({ name, description, enabled }: { name: string; description: string; enabled: boolean }) {
  return (
    <div style={{
      padding: '16px',
      background: 'var(--bg-tertiary)',
      borderRadius: 'var(--radius-md)',
      border: `1px solid ${enabled ? 'var(--accent-success)' : 'var(--border-color)'}`,
    }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '8px' }}>
        <div style={{ fontWeight: 500, color: 'var(--text-primary)' }}>{name}</div>
        <div style={{ 
          fontSize: '11px', 
          padding: '2px 8px', 
          borderRadius: 'var(--radius-sm)',
          background: enabled ? 'rgba(92, 184, 92, 0.2)' : 'rgba(150, 150, 150, 0.2)',
          color: enabled ? 'var(--accent-success)' : 'var(--text-muted)',
        }}>
          {enabled ? 'Enabled' : 'Disabled'}
        </div>
      </div>
      <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>{description}</div>
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
            Configure download clients for torrent or usenet downloads.
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
              Add torrent or usenet download clients.
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

      <SettingsSection title="Download Settings">
        <SettingsField 
          label="Maximum Concurrent Downloads" 
          description="Number of downloads that can run at the same time"
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
          label="Download Timeout (seconds)" 
          description="Maximum time to wait for a download before failing"
        >
          <input 
            className="input" 
            type="number"
            style={{ width: '100px' }}
            defaultValue={300}
            min={30}
          />
        </SettingsField>
        
        <SettingsField label="Retry Failed Downloads">
          <label style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <input type="checkbox" defaultChecked />
            <span style={{ fontSize: '13px' }}>Automatically retry failed downloads</span>
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
  const [testing, setTesting] = useState(false);
  const [testResult, setTestResult] = useState<ProviderTestResult | null>(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const { data: implementations } = useQuery({
    queryKey: ['provider-implementations'],
    queryFn: api.getProviderImplementations,
  });

  const filteredImplementations = implementations?.filter(
    i => i.category === category
  ) ?? [];

  const selectedImpl = filteredImplementations.find(i => i.name === formData.implementation);

  const handleTest = async () => {
    setTesting(true);
    setTestResult(null);
    try {
      const result = provider
        ? await api.testProvider(provider.id)
        : await api.testNewProvider(formData);
      setTestResult(result);
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
      if (provider) {
        await api.updateProvider(provider.id, formData);
      } else if (category === 'Indexer') {
        await api.createIndexer(formData);
      } else {
        await api.createDownloadClient(formData);
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
              placeholder="My DDL Provider"
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

          {selectedImpl?.requiresBaseUrl && (
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
                value={formData.apiKey}
                onChange={(e) => setFormData({ ...formData, apiKey: e.target.value })}
                placeholder="Your API key"
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
