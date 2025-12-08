import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ManHourLogin } from './man-hour-login';

describe('ManHourLogin', () => {
  let component: ManHourLogin;
  let fixture: ComponentFixture<ManHourLogin>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ManHourLogin]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ManHourLogin);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
