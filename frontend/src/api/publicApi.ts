import { httpClient } from './httpClient'

export interface PublicConfig {
  supportEmail: string
}

export const publicApi = {
  async getConfig(): Promise<PublicConfig> {
    const { data } = await httpClient.get<PublicConfig>('/api/public/config')
    return data
  },
}
