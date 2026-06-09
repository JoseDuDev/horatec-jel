'use client'

import { useEffect, useState } from 'react'
import { resourcesApi } from '@/lib/api/resources'
import type { Resource } from '@/lib/types/resource'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { BusinessHoursEditor } from '@/components/availability/BusinessHoursEditor'
import { ResourceRulesEditor } from '@/components/availability/ResourceRulesEditor'
import { ExceptionsEditor } from '@/components/availability/ExceptionsEditor'

export default function DisponibilidadePage() {
  const [resources, setResources] = useState<Resource[]>([])

  useEffect(() => {
    resourcesApi.list().then(setResources)
  }, [])

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold text-slate-900">Disponibilidade</h1>

      <Tabs defaultValue="horarios">
        <TabsList>
          <TabsTrigger value="horarios">Horários Globais</TabsTrigger>
          <TabsTrigger value="grade">Grade por Recurso</TabsTrigger>
          <TabsTrigger value="excecoes">Exceções</TabsTrigger>
        </TabsList>

        <TabsContent value="horarios" className="mt-6">
          <p className="text-sm text-slate-500 mb-6">
            Horários padrão de funcionamento do negócio (se aplica a todos os
            recursos sem grade própria).
          </p>
          <BusinessHoursEditor />
        </TabsContent>

        <TabsContent value="grade" className="mt-6">
          <p className="text-sm text-slate-500 mb-6">
            Grade semanal de cada profissional ou recurso. Quando configurada,
            substitui os horários globais para aquele recurso.
          </p>
          <ResourceRulesEditor resources={resources} />
        </TabsContent>

        <TabsContent value="excecoes" className="mt-6">
          <p className="text-sm text-slate-500 mb-6">
            Bloqueie datas específicas ou defina horários alternativos para um
            dia (folgas, feriados, manutenção).
          </p>
          <ExceptionsEditor resources={resources} />
        </TabsContent>
      </Tabs>
    </div>
  )
}
