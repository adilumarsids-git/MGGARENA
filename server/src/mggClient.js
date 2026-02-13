const API_PREFIX = '/api/v1';

export class MggClient {
  constructor({ baseUrl, apiKey }) {
    this.baseUrl = baseUrl.replace(/\/$/, '');
    this.apiKey = apiKey;
  }

  async call(path, { method = 'POST', body, accessToken, refreshToken }) {
    const execute = async (token) => {
      const headers = { 'Content-Type': 'application/json', Authorization: `Bearer ${this.apiKey}` };
      if (token) headers['X-ACCESS-TOKEN'] = token;
      const response = await fetch(`${this.baseUrl}${API_PREFIX}${path}`, {
        method,
        headers,
        body: body ? JSON.stringify(body) : undefined
      });
      let data = null;
      try { data = await response.json(); } catch (_) { data = null; }
      return { response, data };
    };

    let res = await execute(accessToken);
    if (res.response.status === 401 && refreshToken) {
      const refreshed = await this.refresh(refreshToken);
      if (refreshed?.access_token) {
        res = await execute(refreshed.access_token);
        return { ...res, refreshedTokens: refreshed };
      }
    }
    return res;
  }

  async refresh(refreshToken) {
    const response = await fetch(`${this.baseUrl}${API_PREFIX}/auth/refresh`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${this.apiKey}`
      },
      body: JSON.stringify({ refresh_token: refreshToken })
    });
    if (!response.ok) return null;
    try {
      return await response.json();
    } catch (_) {
      return null;
    }
  }
}
