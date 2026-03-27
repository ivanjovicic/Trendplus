export interface TransferItemDto {
  skuId: number
  skuCode?: string
  quantity: number
  unit?: string
}

export interface TransferCreateRequest {
  sourceId: number
  destinationId: number
  sourceType: 'store'|'warehouse'
  destinationType: 'store'|'warehouse'
  reserve: boolean
  notes?: string
  items: TransferItemDto[]
}

export interface TransferResponse {
  id: number
  status: string
  sourceId: number
  destinationId: number
  reserve: boolean
  items: TransferItemDto[]
  createdAt: string
}

export async function createTransfer(req: TransferCreateRequest): Promise<TransferResponse> {
  const res = await fetch('/transfers', { method: 'POST', headers: {'Content-Type':'application/json'}, body: JSON.stringify(req) })
  if (!res.ok) throw new Error('Failed to create transfer')
  return await res.json()
}

export async function getTransfer(id: number): Promise<TransferResponse> {
  const res = await fetch(`/transfers/${id}`)
  if (!res.ok) throw new Error('Not found')
  return await res.json()
}
