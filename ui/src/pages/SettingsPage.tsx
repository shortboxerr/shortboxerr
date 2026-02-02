import { useState, useRef, useEffect } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { 
  Settings, Server, Download, Shield, 
  FolderOpen, Plug, Save, Plus, Edit, Trash2, 
  CheckCircle, XCircle, AlertCircle, Play, GripVertical,
  Eye, EyeOff, Copy, RefreshCw, X
} from 'lucide-react';
import { api } from '../api/client';
import type { Provider, CreateProviderRequest, ProviderTestResult } from '../api/client';
import { useTheme } from '../App';

type SettingsTab = 'general' | 'indexers' | 'download' | 'import' | 'ui' | 'security';

const tabs: { id: SettingsTab; icon: React.ElementType; label: string }[] = [
  { id: 'general', icon: Settings, label: 'General' },
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
  
  const seriesInputRef = useRef<HTMLInputElement>(null);
  const issueInputRef = useRef<HTMLInputElement>(null);
  const collectionInputRef = useRef<HTMLInputElement>(null);

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
    </>
  );
}

// ============== INDEXERS SETTINGS ==============

function IndexersSettings() {
  const [showModal, setShowModal] = useState(false);
  const [editingProvider, setEditingProvider] = useState<Provider | null>(null);
  const queryClient = useQueryClient();

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
      <SettingsSection title="DDL Indexers">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
          <p style={{ color: 'var(--text-muted)', fontSize: '13px', margin: 0 }}>
            Configure DDL providers to discover and download comics.
          </p>
          <div style={{ display: 'flex', gap: '8px' }}>
            <button className="btn btn-icon" onClick={() => refetch()} title="Refresh">
              <RefreshCw size={16} />
            </button>
            <button className="btn btn-primary" onClick={handleAdd}>
              <Plus size={16} />
              Add Indexer
            </button>
          </div>
        </div>

        {isLoading ? (
          <div className="loading"><div className="spinner" /></div>
        ) : !indexers?.length ? (
          <div className="empty-state" style={{ padding: '40px 20px' }}>
            <Plug size={48} />
            <div className="empty-state-title">No indexers configured</div>
            <div className="empty-state-text">
              Add DDL providers like GetComics to discover new comics.
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
            Configure HTTP or other download clients.
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
              Add HTTP or other download clients.
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
  const [showApiKey, setShowApiKey] = useState(false);
  const [apiKey] = useState('sk_live_abc123def456ghi789jkl012mno345pqr678');

  const handleCopyApiKey = () => {
    navigator.clipboard.writeText(apiKey);
  };

  return (
    <>
      <SettingsSection title="Authentication">
        <SettingsField label="Require Authentication">
          <label style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <input type="checkbox" />
            <span style={{ fontSize: '13px' }}>Require login to access the UI</span>
          </label>
        </SettingsField>
        
        <SettingsField label="Username">
          <input className="input" style={{ width: '250px' }} />
        </SettingsField>
        
        <SettingsField label="Password">
          <input className="input" type="password" style={{ width: '250px' }} />
        </SettingsField>
      </SettingsSection>
      
      <SettingsSection title="API">
        <SettingsField 
          label="API Key" 
          description="Used for external integrations"
        >
          <div style={{ display: 'flex', gap: '8px' }}>
            <input 
              className="input" 
              style={{ flex: 1, fontFamily: 'var(--font-mono)' }}
              value={showApiKey ? apiKey : '•'.repeat(40)}
              readOnly
            />
            <button 
              className="btn btn-icon" 
              onClick={() => setShowApiKey(!showApiKey)}
              title={showApiKey ? 'Hide' : 'Show'}
            >
              {showApiKey ? <EyeOff size={16} /> : <Eye size={16} />}
            </button>
            <button 
              className="btn btn-icon" 
              onClick={handleCopyApiKey}
              title="Copy"
            >
              <Copy size={16} />
            </button>
            <button className="btn btn-secondary">Regenerate</button>
          </div>
        </SettingsField>
      </SettingsSection>
    </>
  );
}
