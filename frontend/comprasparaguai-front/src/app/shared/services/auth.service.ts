import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';

interface LoginResponse {
  token: string;
  usuario: string;
  nombre: string;
  vence: string;
}

const CLAVE = 'cp_sesion';

/**
 * Sesión del panel. El token JWT se guarda en localStorage (puede no estar disponible: siempre en try/catch)
 * y lo agrega el interceptor a cada llamada a la API. Ver datos es libre; escribir exige sesión.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private apiUrl = `http://${window.location.hostname}:5000/api`;

  token: string | null = null;
  usuario: string | null = null;
  nombre: string | null = null;

  /** El interceptor lo enciende cuando la API responde 401; AppComponent abre el login. */
  pedirLogin = false;

  constructor(private http: HttpClient) {
    this.restaurar();
  }

  get logueado(): boolean {
    return !!this.token;
  }

  login(usuario: string, password: string): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.apiUrl}/auth/login`, { usuario, password }).pipe(
      tap(r => {
        this.token = r.token;
        this.usuario = r.usuario;
        this.nombre = r.nombre;
        this.pedirLogin = false;
        this.guardar(r);
      })
    );
  }

  logout() {
    this.token = this.usuario = this.nombre = null;
    try { localStorage.removeItem(CLAVE); } catch { /* sin almacenamiento */ }
  }

  private guardar(r: LoginResponse) {
    try { localStorage.setItem(CLAVE, JSON.stringify(r)); } catch { /* sin almacenamiento */ }
  }

  private restaurar() {
    try {
      const raw = localStorage.getItem(CLAVE);
      if (!raw) return;
      const r: LoginResponse = JSON.parse(raw);
      if (r.vence && new Date(r.vence).getTime() < Date.now()) {
        localStorage.removeItem(CLAVE);
        return;
      }
      this.token = r.token;
      this.usuario = r.usuario;
      this.nombre = r.nombre;
    } catch { /* sin almacenamiento o dato corrupto */ }
  }
}
