import { ComponentFixture, TestBed } from '@angular/core/testing';

import { MachineEmployee } from './machine-employee';

describe('MachineEmployee', () => {
  let component: MachineEmployee;
  let fixture: ComponentFixture<MachineEmployee>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MachineEmployee]
    })
    .compileComponents();

    fixture = TestBed.createComponent(MachineEmployee);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
