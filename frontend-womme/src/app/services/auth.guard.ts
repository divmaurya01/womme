import { Injectable } from '@angular/core';
import { CanActivate, Router, ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';
import { AuthServices } from '../services/auth.service';

@Injectable({ providedIn: 'root' })
export class AuthGuard implements CanActivate {
  constructor(private auth: AuthServices, private router: Router) {}

  canActivate(route: ActivatedRouteSnapshot, state: RouterStateSnapshot): boolean {
    const token = localStorage.getItem('token');
    const randomToken = localStorage.getItem('randomToken');
    const role = localStorage.getItem('role');
    const userDetails = localStorage.getItem('userDetails');
    const permissions = localStorage.getItem('permissions');
    const ss_id = route.queryParams['ss_id'];

    const isValid =
      !!token &&
      !!randomToken &&
      !!role &&
      !!userDetails &&
      !!permissions;

    if (!isValid) {
      console.warn('AuthGuard → Invalid or missing auth data in localStorage');
      localStorage.clear();
      this.router.navigate(['/']);
      return false;
    }

    // Debug logs to verify values
    console.log('AuthGuard Check →', { ss_id, randomToken });

    // Compare ss_id from URL with randomToken from localStorage
    if (!ss_id || ss_id !== randomToken) {
      console.warn('AuthGuard → Invalid or missing session token', { ss_id, randomToken });
      this.router.navigate(['/']);
      return false;
    }

    // ✅ Token expiry check (only if token is JWT-like)
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      const expiry = payload.exp;
      const now = Math.floor(Date.now() / 1000);
      if (expiry && expiry < now) {
        console.warn('AuthGuard → Token expired');
        localStorage.clear();
        this.router.navigate(['/']);
        return false;
      } 
    } catch (e) {
      console.error('AuthGuard → Invalid token format', e);
      localStorage.clear();
      this.router.navigate(['/']);
      return false;
    }

    return true;
  }
}
