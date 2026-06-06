'use client'

import { useState } from 'react'
import type { OnboardingThemeData } from '@/lib/api/onboarding'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

const PRESET_COLORS = [
  '#6366f1', '#8b5cf6', '#ec4899', '#f97316',
  '#10b981', '#3b82f6', '#ef4444', '#1e293b',
]

interface Props {
  onNext: (data: OnboardingThemeData) => void
  onBack: () => void
}

export function OnboardingStepTheme({ onNext, onBack }: Props) {
  const [color, setColor] = useState('#6366f1')
  const [logoUrl, setLogoUrl] = useState('')

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-bold mb-1">Identidade Visual</h2>
        <p className="text-sm text-slate-500 mb-6">Escolha a cor principal e adicione seu logo.</p>
      </div>

      <div>
        <Label>Cor Principal</Label>
        <div className="flex gap-2 mt-2 flex-wrap">
          {PRESET_COLORS.map(c => (
            <button
              key={c}
              type="button"
              onClick={() => setColor(c)}
              className="h-9 w-9 rounded-full border-2 transition-all"
              style={{
                backgroundColor: c,
                borderColor: color === c ? '#1e293b' : 'transparent',
              }}
            />
          ))}
          <input
            type="color"
            value={color}
            onChange={e => setColor(e.target.value)}
            className="h-9 w-9 rounded-full border cursor-pointer"
          />
        </div>
        <div className="mt-3 p-3 rounded-lg text-white text-sm font-medium" style={{ backgroundColor: color }}>
          Prévia da cor selecionada
        </div>
      </div>

      <div>
        <Label htmlFor="logoUrl">URL do Logo (opcional)</Label>
        <Input
          id="logoUrl"
          value={logoUrl}
          onChange={e => setLogoUrl(e.target.value)}
          placeholder="https://exemplo.com/logo.png"
        />
        {logoUrl && (
          // eslint-disable-next-line @next/next/no-img-element
          <img src={logoUrl} alt="Preview" className="mt-2 h-12 object-contain" onError={() => setLogoUrl('')} />
        )}
      </div>

      <div className="flex gap-3">
        <Button variant="outline" onClick={onBack} className="flex-1">← Voltar</Button>
        <Button
          onClick={() => onNext({ primaryColor: color, logoUrl: logoUrl || undefined })}
          className="flex-1"
        >
          Próximo →
        </Button>
      </div>
    </div>
  )
}
