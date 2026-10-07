import React from 'react'

export interface TransferItem {
  skuId: number
  code: string
  name: string
  quantity: number
}

interface Props {
  items: TransferItem[]
  onChange: (items: TransferItem[]) => void
}

const TransferItemsTable: React.FC<Props> = ({ items }) => {
  return (
    <div className="rounded-2xl border p-4 bg-[var(--surface-elevated)] mt-4">
      <h3 className="font-semibold mb-2">Stavke</h3>
      <div className="max-w-full overflow-x-auto overscroll-x-contain" role="region" aria-label="Stavke za prenos" tabIndex={0}>
      <table className="min-w-[420px] w-full text-sm">
        <thead>
          <tr>
            <th className="sticky left-0 z-10 bg-[var(--surface-elevated)] text-left">Šifra</th>
            <th>Naziv</th>
            <th className="text-right">Količina</th>
          </tr>
        </thead>
        <tbody>
          {items.length === 0 ? (
            <tr><td colSpan={3} className="py-4 text-center text-[var(--text-muted)]">Nema stavki</td></tr>
          ) : (
            items.map(i => (
              <tr key={i.skuId}>
                <td className="sticky left-0 z-10 bg-[var(--surface-elevated)]">{i.code}</td>
                <td className="break-words">{i.name}</td>
                <td className="text-right">{i.quantity}</td>
              </tr>
            ))
          )}
        </tbody>
      </table>
      </div>
    </div>
  )
}

export default TransferItemsTable
