// Compatibility shim — the canonical axios instance lives at services/api.ts now.
// Existing imports of `@/api/http` (or `../api/http`) continue to work.
import api from '../services/api'

export const http = api
export default api
