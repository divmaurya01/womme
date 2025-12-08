import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Router } from '@angular/router';

@Injectable({
  providedIn: 'root'
})
export class AuthServices {
  private readonly baseURL = 'http://localhost:5173'; 
  private jwtToken: string = '';

  constructor(private http: HttpClient, private router: Router) {
    // Initialize from localStorage if present
    const token = localStorage.getItem('token');
    if (token) {
      this.jwtToken = token;
    }
  }

  /**
   *  Login API call
   * @param employeeCode - User login ID
   * @param password - User password
   */
  login(employeeCode: string, password: string): Observable<any> {
    const body = {
      emp_num: employeeCode,
      PasswordHash: password
    };
    return this.http.post<any>(`${this.baseURL}/api/Auth/login`, body);
  }
  

  
  setJwtToken(token: string): void {
    this.jwtToken = token;
    localStorage.setItem('token', token);
  }

 
  getJwtToken(): string {
    return this.jwtToken || localStorage.getItem('token') || '';
  }

  
  setUserDetails(user: any): void {
    localStorage.setItem('userDetails', JSON.stringify(user));
  }

 
  getUserDetails(): any {
    const user = localStorage.getItem('userDetails');
    return user ? JSON.parse(user) : null;
  }

  
  isLoggedIn(): boolean {
    return !!localStorage.getItem('token');
  }


  logout(): void {
    this.jwtToken = '';
    localStorage.removeItem('token');
    localStorage.removeItem('userDetails');
    localStorage.clear(); 
    this.router.navigate(['/']);
  }

}
