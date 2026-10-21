import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

/** Agrega Authorization: Bearer a las llamadas a la API; ante un 401 con sesión vencida pide volver a entrar. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const esLogin = req.url.endsWith('/auth/login');

  const conToken = auth.token && !esLogin
    ? req.clone({ setHeaders: { Authorization: `Bearer ${auth.token}` } })
    : req;

  return next(conToken).pipe(
    catchError((e: HttpErrorResponse) => {
      if (e.status === 401 && !esLogin) {
        auth.logout();
        auth.pedirLogin = true;
      }
      return throwError(() => e);
    })
  );
};
