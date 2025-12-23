import { ComponentFixture, TestBed } from '@angular/core/testing';

import { VerifyTransaction } from './verify-transaction';

describe('VerifyTransaction', () => {
  let component: VerifyTransaction;
  let fixture: ComponentFixture<VerifyTransaction>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [VerifyTransaction]
    })
    .compileComponents();

    fixture = TestBed.createComponent(VerifyTransaction);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
