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
      <table className="w-full text-sm">
        <thead>
          <tr>
            <th>Šifra</th>
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
                <td>{i.code}</td>
                <td>{i.name}</td>
                <td className="text-right">{i.quantity}</td>
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  )
}

export default TransferItemsTable
