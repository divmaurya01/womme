import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DashboardVerify } from './dashboard-verify';

describe('DashboardVerify', () => {
  let component: DashboardVerify;
  let fixture: ComponentFixture<DashboardVerify>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DashboardVerify]
    })
    .compileComponents();

    fixture = TestBed.createComponent(DashboardVerify);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
