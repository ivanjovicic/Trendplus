import React from 'react'

const TransferUploadCsv: React.FC = () => {
  const onFile = (e: React.ChangeEvent<HTMLInputElement>) => {
    const f = e.target.files?.[0]
    if (!f) return
    // lightweight parser placeholder
    const reader = new FileReader()
    reader.onload = () => {
      // parse CSV and show preview (implementation needed)
      console.log('CSV contents', reader.result)
    }
    reader.readAsText(f)
  }

  return (
    <div className="rounded-2xl border p-4 bg-[var(--surface-elevated)] mb-4">
      <h4 className="font-semibold mb-2">Učitaj CSV</h4>
      <input type="file" accept=".csv" onChange={onFile} />
    </div>
  )
}

export default TransferUploadCsv
