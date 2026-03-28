import api from './api';

export async function getRestoreScript(ids: number[]): Promise<string> {
  const res = await api.post('/api/access-import/cleanup/archive/restore-script', { ids });
  return res.data?.script ?? '';
}
