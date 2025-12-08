import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthServices } from '../../services/auth.service';
import { JobService } from '../../services/job.service';
import { LoaderService } from '../../services/loader.service';
import { finalize } from 'rxjs/operators';
@Component({
  selector: 'man-hour-login',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './man-hour-login.html',
  styleUrls: ['./man-hour-login.scss']
})
export class ManHourLoginComponent {
  userID: string = '';
  password: string = '';
  errorMessage: string = '';
  successMessage: string = '';
  submitted: boolean = false;
  shake = false;
  showPassword: boolean = false;

  constructor(
    private router: Router,
    private authService: AuthServices,
    private jobService:JobService,
    private loader:LoaderService,
    
  ) {}
ngOnInit(): void {
  localStorage.clear(); 
  
}


togglePassword(): void {
  this.showPassword = !this.showPassword;
}

  login() {
  this.submitted = true;
  this.errorMessage = '';
  this.successMessage = '';

  if (!this.userID || !this.password) {
    this.triggerShake();
    this.errorMessage = 'Please fill out both Employee ID and Password.';
    return;
  }

  this.loader.show();
  this.authService.login(this.userID, this.password)
  .pipe(finalize(() => this.loader.hide()))
  .subscribe({
    next: (res) => {
      const randomToken = res.randomToken;
      const userDetails = res.userDetails;

      // Save to local storage
      localStorage.setItem('randomToken', randomToken);
      localStorage.setItem('userDetails', JSON.stringify(userDetails));
      localStorage.setItem('role', userDetails.roleName);
      this.authService.setJwtToken(res.jwtToken); // Save JWT in memory

      // Get permissions for the role and redirect accordingly
      this.jobService.getPagePermissionsByRole(userDetails.roleID)
       .pipe(finalize(() => this.loader.hide())) 
      .subscribe({
        next: (permissions) => {
                  // console.log("clicked",permissions)

          localStorage.setItem('permissions', JSON.stringify(permissions));


          console.log('Permissions list:', permissions);

          permissions.forEach((p, i) => {
            console.log(`Permission[${i}] =>`, p, '| pageURL:', p.pageUrl);
          });
          const firstAllowedPage = permissions.find(p => p.pageUrl);
          
          console.log('first page allowed',firstAllowedPage)
          if (firstAllowedPage) {
            this.loader.hide();
            this.router.navigate([firstAllowedPage.pageUrl], {
              queryParams: { ss_id: randomToken }
            });
          } else {
            this.loader.hide();
            this.errorMessage = 'No pages assigned to this role.';
          }
        },
        error: () => {
          this.loader.hide();
          this.errorMessage = 'Failed to fetch permissions.';
        }
      });
    },
    error: (err) => {
      this.loader.hide();
      this.triggerShake();
      this.errorMessage = 'Invalid credentials. Please try again.';
      console.error('Login failed:', err);
    }
  });
}

  private triggerShake() {
    this.shake = true;
    setTimeout(() => (this.shake = false), 500);
  }
}
