import { useState } from 'react';
import { 
  Settings, Server, Download, Shield, 
  FolderOpen, Plug, Save
} from 'lucide-react';

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
            {activeTab === 'download' && <DownloadSettings />}
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

function GeneralSettings() {
  return (
    <>
      <SettingsSection title="Naming">
        <SettingsField 
          label="Series Folder Format" 
          description="Pattern for organizing series folders"
        >
          <input 
            className="input" 
            style={{ width: '100%' }}
            defaultValue="{Series Title} ({Year})"
          />
        </SettingsField>
        
        <SettingsField 
          label="Issue File Format" 
          description="Pattern for naming issue files"
        >
          <input 
            className="input" 
            style={{ width: '100%' }}
            defaultValue="{Series Title} #{Issue} ({Year})"
          />
        </SettingsField>
        
        <SettingsField 
          label="Collection File Format" 
          description="Pattern for naming collection files"
        >
          <input 
            className="input" 
            style={{ width: '100%' }}
            defaultValue="{Series Title} - {Edition Type} Vol. {Volume} ({Year})"
          />
        </SettingsField>
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
          label="Staging Folder" 
          description="Where downloaded files are placed for review"
        >
          <div style={{ display: 'flex', gap: '8px' }}>
            <input 
              className="input" 
              style={{ flex: 1 }}
              defaultValue="/staging"
            />
            <button className="btn btn-secondary">Browse</button>
          </div>
        </SettingsField>
      </SettingsSection>
    </>
  );
}

function IndexersSettings() {
  return (
    <SettingsSection title="Indexers">
      <div className="empty-state" style={{ padding: '40px 20px' }}>
        <Plug size={48} />
        <div className="empty-state-title">No indexers configured</div>
        <div className="empty-state-text">
          Add DDL providers, RSS feeds, or other indexers to discover new comics.
        </div>
        <button className="btn btn-primary" style={{ marginTop: '16px' }}>
          Add Indexer
        </button>
      </div>
    </SettingsSection>
  );
}

function DownloadSettings() {
  return (
    <>
      <SettingsSection title="Download Clients">
        <div className="empty-state" style={{ padding: '40px 20px' }}>
          <Download size={48} />
          <div className="empty-state-title">No download clients configured</div>
          <div className="empty-state-text">
            Add HTTP, torrent, or other download clients.
          </div>
          <button className="btn btn-primary" style={{ marginTop: '16px' }}>
            Add Download Client
          </button>
        </div>
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
    </>
  );
}

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
  return (
    <SettingsSection title="UI Preferences">
      <SettingsField label="Theme">
        <select className="input" style={{ minWidth: '150px' }}>
          <option value="dark">Dark</option>
          <option value="light">Light</option>
          <option value="auto">System</option>
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
              defaultValue="********************************"
              readOnly
            />
            <button className="btn btn-secondary">Regenerate</button>
          </div>
        </SettingsField>
      </SettingsSection>
    </>
  );
}

