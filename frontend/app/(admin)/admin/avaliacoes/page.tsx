'use client'

import { useCallback, useEffect, useState } from 'react'
import { format } from 'date-fns'
import { ptBR } from 'date-fns/locale'
import { Star } from 'lucide-react'
import { resourcesApi } from '@/lib/api/resources'
import { reviewsApi } from '@/lib/api/reviews'
import type { Resource } from '@/lib/types/resource'
import type { ResourceReviewsResult, ReviewItem } from '@/lib/types/review'
import { Card, CardContent } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select'

function Stars({ value }: { value: number }) {
  return (
    <div className="flex items-center gap-1">
      {Array.from({ length: 5 }, (_, i) => (
        <Star
          key={i}
          className={`h-4 w-4 ${i < value ? 'fill-amber-400 text-amber-400' : 'text-slate-200'}`}
        />
      ))}
    </div>
  )
}

function ReplyBox({ review, onReplied }: { review: ReviewItem; onReplied: () => void }) {
  const [text, setText] = useState('')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const submit = async () => {
    if (!text.trim()) return
    setError(null)
    setSaving(true)
    try {
      await reviewsApi.reply(review.id, text.trim())
      onReplied()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Falha ao responder')
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="mt-3 space-y-2">
      <textarea
        value={text}
        onChange={e => setText(e.target.value)}
        rows={2}
        placeholder="Escreva uma resposta pública..."
        className="w-full rounded-md border border-slate-200 px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-300"
      />
      <div className="flex items-center gap-3">
        <Button size="sm" onClick={submit} disabled={saving || !text.trim()}>
          {saving ? 'Enviando...' : 'Responder'}
        </Button>
        {error && <span className="text-sm text-red-500">{error}</span>}
      </div>
    </div>
  )
}

export default function AvaliacoesPage() {
  const [resources, setResources] = useState<Resource[]>([])
  const [resourceId, setResourceId] = useState('')
  const [data, setData] = useState<ResourceReviewsResult | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    resourcesApi.list().then(rs => {
      setResources(rs)
      if (rs.length > 0) setResourceId(rs[0].id)
      else setLoading(false)
    })
  }, [])

  const load = useCallback((id: string) => {
    if (!id) return
    reviewsApi.byResource(id)
      .then(setData)
      .finally(() => setLoading(false))
  }, [])

  useEffect(() => { load(resourceId) }, [resourceId, load])

  const handleResourceChange = (v: string) => {
    setLoading(true)
    setData(null)
    setResourceId(v ?? '')
  }

  const reviews = data?.page.items ?? []

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold text-slate-900">Avaliações</h1>

      <div className="max-w-xs">
        <Select value={resourceId} onValueChange={handleResourceChange}>
          <SelectTrigger>
            <SelectValue placeholder="Selecione um recurso...">
              {(value) => resources.find(r => r.id === value)?.name ?? 'Selecione um recurso...'}
            </SelectValue>
          </SelectTrigger>
          <SelectContent>
            {resources.map(r => (
              <SelectItem key={r.id} value={r.id}>{r.name}</SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {data && (
        <div className="flex items-center gap-3 text-sm text-slate-600">
          <Stars value={Math.round(data.averageStars)} />
          <span className="font-medium">{data.averageStars.toFixed(1)}</span>
          <span className="text-slate-400">·</span>
          <span>{data.totalReviews} avaliação(ões)</span>
        </div>
      )}

      {loading ? (
        <p className="text-slate-500">Carregando...</p>
      ) : reviews.length === 0 ? (
        <p className="text-slate-400">Nenhuma avaliação para este recurso.</p>
      ) : (
        <div className="space-y-4 max-w-2xl">
          {reviews.map(r => (
            <Card key={r.id}>
              <CardContent className="pt-4 space-y-2">
                <div className="flex items-center justify-between">
                  <Stars value={r.stars} />
                  <span className="text-xs text-slate-400">
                    {format(new Date(r.createdAt), "dd 'de' MMMM 'de' yyyy", { locale: ptBR })}
                  </span>
                </div>
                {r.comment && <p className="text-sm text-slate-700">{r.comment}</p>}

                {r.ownerReply ? (
                  <div className="mt-2 rounded-md bg-slate-50 border border-slate-100 p-3">
                    <p className="text-xs font-medium text-slate-500 mb-1">Sua resposta</p>
                    <p className="text-sm text-slate-700">{r.ownerReply}</p>
                    {r.ownerRepliedAt && (
                      <p className="text-xs text-slate-400 mt-1">
                        {format(new Date(r.ownerRepliedAt), "dd/MM/yyyy", { locale: ptBR })}
                      </p>
                    )}
                  </div>
                ) : (
                  <ReplyBox review={r} onReplied={() => load(resourceId)} />
                )}
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  )
}
