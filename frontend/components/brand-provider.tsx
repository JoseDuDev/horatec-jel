'use client'

import { createContext, useContext } from 'react'
import { DEFAULT_BRAND, type Brand } from '@/lib/brand'

const BrandContext = createContext<Brand>(DEFAULT_BRAND)

/** Disponibiliza a marca resolvida no servidor para os Client Components. */
export function BrandProvider({ brand, children }: { brand: Brand; children: React.ReactNode }) {
  return <BrandContext.Provider value={brand}>{children}</BrandContext.Provider>
}

/** Marca de produto ativa (resolvida pelo host). */
export function useBrand(): Brand {
  return useContext(BrandContext)
}
