import { Component } from '@angular/core';

@Component({
  selector: 'app-forgot-password',
  templateUrl: './forgot-password.html',
  styleUrls: ['./forgot-password.scss']
})
export class ForgotPasswordComponent {
  email: string = '';

  onSubmit() {
    if (this.email) {
      alert(`Reset link sent to ${this.email}`);
      // Call your backend API here
    } else {
      alert('Please enter your email address.');
    }
  }

 
}
