import React, { useState } from 'react'

const TransferForm: React.FC = () => {
  const [sourceType, setSourceType] = useState<'store'|'warehouse'>('store')
  const [reserve, setReserve] = useState(true)

  return (
    <div className="rounded-2xl border p-5 bg-[var(--surface-elevated)]">
      <h2 className="text-lg font-semibold mb-4">Prenos robe</h2>
      <div className="space-y-4">
        <div className="grid grid-cols-2 gap-3">
          <select className="w-full form-select">
            <option value="store">Prodavnica</option>
            <option value="warehouse">Magacin</option>
          </select>
          <select className="w-full form-select">
            <option value="store">Prodavnica</option>
            <option value="warehouse">Magacin</option>
          </select>
        </div>

        <label className="inline-flex items-center gap-2">
          <input type="checkbox" checked={reserve} onChange={e => setReserve(e.target.checked)} />
          <span>Rezerviši količine</span>
        </label>

        <div>
          <button className="btn btn-primary">Dodaj artikal</button>
          <button className="ml-2 btn">Pregled</button>
        </div>
      </div>
    </div>
  )
}

export default TransferForm
