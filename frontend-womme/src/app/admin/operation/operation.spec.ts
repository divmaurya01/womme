import { ComponentFixture, TestBed } from '@angular/core/testing';

import { Operation } from './operation';

describe('Operation', () => {
  let component: Operation;
  let fixture: ComponentFixture<Operation>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Operation]
    })
    .compileComponents();

    fixture = TestBed.createComponent(Operation);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
