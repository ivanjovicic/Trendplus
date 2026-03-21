import { Check, Palette } from 'lucide-react';
import { useTheme } from '../context/ThemeContext';

export default function ThemeSettingsPage() {
  const { currentTheme, themes, setTheme } = useTheme();

  return (
    <div className="min-h-screen surface p-6">
      <div className="mx-auto max-w-4xl">
        {/* Header */}
        <div className="mb-8">
          <div className="flex items-center gap-3 mb-4">
            <div className="rounded-2xl border border-muted bg-surface-elevated p-2.5 text-contrast">
              <Palette size={20} />
            </div>
            <div>
              <h1 className="text-2xl font-semibold text-contrast">Podešavanja tema</h1>
              <p className="text-muted text-sm">Prilagodi izgled aplikacije prema svojim potrebama</p>
            </div>
          </div>
        </div>

        {/* Current Theme Info */}
        <div className="mb-8 rounded-2xl border border-muted surface-light p-6">
          <h2 className="text-lg font-semibold text-contrast mb-2">Trenutna tema</h2>
          <div className="flex items-center gap-3">
            <div className="flex h-12 w-12 items-center justify-center rounded-xl border border-muted surface-elevated">
              <Check size={20} className="text-green-400" />
            </div>
            <div>
              <div className="font-medium text-contrast">{themes[currentTheme].displayName}</div>
              <div className="text-sm text-muted">{themes[currentTheme].description}</div>
            </div>
          </div>
        </div>

        {/* Theme Options */}
        <div className="space-y-4">
          <h2 className="text-lg font-semibold text-contrast mb-4">Dostupne teme</h2>
          
          {Object.values(themes).map((theme) => (
            <button
              key={theme.name}
              onClick={() => setTheme(theme.name)}
              className={`w-full rounded-2xl border p-6 text-left transition-all duration-200 ${ 
                currentTheme === theme.name
                  ? 'border-focus-ring bg-surface-elevated ring-2 ring-focus-ring ring-opacity-20'
                  : 'border-muted surface-light hover:border-hover hover:surface-elevated'
              }`}
            >
              <div className="flex items-start justify-between">
                <div className="flex-1">
                  <div className="flex items-center gap-3 mb-2">
                    <h3 className="font-semibold text-contrast">{theme.displayName}</h3>
                    {currentTheme === theme.name && (
                      <Check size={16} className="text-green-400" />
                    )}
                  </div>
                  <p className="text-sm text-muted mb-4">{theme.description}</p>
                  
                  {/* Theme Preview */}
                  <div className="flex gap-2">
                    <div 
                      className="h-8 w-12 rounded border"
                      style={{ backgroundColor: theme.cssVars['--surface-default'] }}
                    />
                    <div 
                      className="h-8 w-12 rounded border"
                      style={{ backgroundColor: theme.cssVars['--surface-light'] }}
                    />
                    <div 
                      className="h-8 w-12 rounded border"
                      style={{ backgroundColor: theme.cssVars['--surface-elevated'] }}
                    />
                    <div className="ml-2 flex items-center gap-1">
                      <div 
                        className="h-3 w-3 rounded-full"
                        style={{ backgroundColor: theme.cssVars['--text-primary'] }}
                      />
                      <div 
                        className="h-3 w-3 rounded-full"
                        style={{ backgroundColor: theme.cssVars['--text-secondary'] }}
                      />
                      <div 
                        className="h-3 w-3 rounded-full"
                        style={{ backgroundColor: theme.cssVars['--text-muted'] }}
                      />
                    </div>
                  </div>
                </div>
              </div>
            </button>
          ))}
        </div>

        {/* Accessibility Info */}
        <div className="mt-8 rounded-2xl border border-muted surface-light p-6">
          <h3 className="font-semibold text-contrast mb-2">Smernice za pristupačnost</h3>
          <div className="space-y-2 text-sm text-secondary">
            <p>• <strong>Bilans Stanja (Tamna):</strong> Optimizovana za rad u tamnim uslovima i dugotrajno čitanje</p>
            <p>• <strong>Svetla:</strong> Klasična tema pogodna za rad u osvetljenim prostorima</p>
            <p>• <strong>Visoki kontrast:</strong> Maksimalni kontrast teksta za lakše čitanje (WCAG AAA)</p>
          </div>
          <div className="mt-4 text-xs text-muted">
            Sve teme su dizajnirane da zadovolje WCAG AA standarde za pristupačnost.
          </div>
        </div>

        {/* Additional Settings Preview */}
        <div className="mt-8 rounded-2xl border border-muted surface-light p-6">
          <h3 className="font-semibold text-contrast mb-4">Test komponenti sa trenutnom temom</h3>
          
          {/* Sample Controls */}
          <div className="space-y-4">
            {/* Input */}
            <div>
              <label className="block text-sm font-medium text-secondary mb-2">Test input</label>
              <input 
                type="text" 
                placeholder="Unesite tekst..." 
                className="control-muted w-full rounded-lg px-3 py-2 text-sm focus:outline-none"
              />
            </div>

            {/* Select */}
            <div>
              <label className="block text-sm font-medium text-secondary mb-2">Test select</label>
              <select className="dark-select control-muted w-full rounded-lg px-3 py-2 text-sm focus:outline-none">
                <option>Opcija 1</option>
                <option>Opcija 2</option>
                <option>Opcija 3</option>
              </select>
            </div>

            {/* Button */}
            <div>
              <label className="block text-sm font-medium text-secondary mb-2">Test dugme</label>
              <button className="rounded-lg border border-muted surface-elevated px-4 py-2 text-sm font-medium text-contrast hover:border-hover hover:surface transition-all duration-200 focus:outline-none focus:ring-2 focus:ring-focus-ring focus:ring-opacity-20">
                Primer dugmeta
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}