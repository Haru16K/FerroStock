import { Component, OnInit, ChangeDetectorRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../core/auth.service';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive],
  template: `
<style>
  *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
  :host { display: flex; min-height: 100vh; font-family: 'Plus Jakarta Sans', sans-serif; background: #F4F3EF; }

  .sidebar { width: 220px; flex-shrink: 0; background: #fff; border-right: 1px solid #E2E0D8; display: flex; flex-direction: column; min-height: 100vh; position: sticky; top: 0; height: 100vh; overflow-y: auto; }
  .brand { padding: 20px 18px 16px; border-bottom: 1px solid #E2E0D8; }
  .brand-logo { display: flex; align-items: center; gap: 9px; }
  .brand-icon { width: 32px; height: 32px; background: #D95F2B; border-radius: 6px; display: flex; align-items: center; justify-content: center; }
  .brand-icon svg { width: 18px; height: 18px; stroke: #fff; fill: none; stroke-width: 2; stroke-linecap: round; stroke-linejoin: round; }
  .brand-name { font-size: 15px; font-weight: 600; color: #1A1916; }
  .brand-sub  { font-size: 11px; color: #A8A69E; }
  .nav-section { font-size: 10px; font-weight: 600; color: #A8A69E; letter-spacing: .08em; text-transform: uppercase; padding: 14px 18px 5px; }
  .nav-item { display: flex; align-items: center; gap: 10px; padding: 8px 18px; font-size: 13px; color: #6B6963; cursor: pointer; border-left: 2px solid transparent; text-decoration: none; }
  .nav-item svg { width: 16px; height: 16px; stroke: currentColor; fill: none; stroke-width: 2; stroke-linecap: round; stroke-linejoin: round; flex-shrink: 0; }
  .nav-item.active { background: #FAE9DF; color: #D95F2B; border-left-color: #D95F2B; font-weight: 600; }
  .nav-item:hover:not(.active) { background: #F4F3EF; }
  .nav-badge { margin-left: auto; background: #FCEBEB; color: #A32D2D; font-size: 10px; font-weight: 600; padding: 1px 7px; border-radius: 99px; }
  .sidebar-footer { margin-top: auto; padding: 14px 18px; border-top: 1px solid #E2E0D8; }
  .user-row { display: flex; align-items: center; gap: 10px; }
  .avatar { width: 32px; height: 32px; border-radius: 50%; background: #E6F1FB; color: #185FA5; font-size: 12px; font-weight: 600; display: flex; align-items: center; justify-content: center; flex-shrink: 0; }
  .user-name { font-size: 12px; font-weight: 600; color: #1A1916; }
  .user-role { font-size: 11px; color: #A8A69E; }
  .btn-logout { width: 100%; margin-top: 10px; padding: 7px; display: flex; align-items: center; justify-content: center; gap: 6px; background: #FCEBEB; color: #A32D2D; border: 1px solid #F7C1C1; border-radius: 6px; font-size: 12px; font-weight: 600; cursor: pointer; font-family: inherit; }
  .btn-logout svg { width: 14px; height: 14px; stroke: currentColor; fill: none; stroke-width: 2; stroke-linecap: round; stroke-linejoin: round; }
  .main { flex: 1; min-width: 0; }
</style>

<aside class="sidebar">
  <div class="brand">
    <div class="brand-logo">
      <div class="brand-icon">
        <svg viewBox="0 0 24 24"><path d="M5 8h14M5 8a2 2 0 110-4h14a2 2 0 110 4M5 8l1 12a2 2 0 002 2h8a2 2 0 002-2L19 8M10 12v6M14 12v6"/></svg>
      </div>
      <div>
        <div class="brand-name">FerroStock</div>
        <div class="brand-sub">FerroTech S.A.C.</div>
      </div>
    </div>
  </div>

  <span class="nav-section">Principal</span>
  <a class="nav-item" *ngIf="puede('Administrador', 'Almacenero', 'Gerencia')" routerLink="/dashboard" routerLinkActive="active">
    <svg viewBox="0 0 24 24"><rect x="3" y="3" width="7" height="7" rx="1"/><rect x="14" y="3" width="7" height="7" rx="1"/><rect x="3" y="14" width="7" height="7" rx="1"/><rect x="14" y="14" width="7" height="7" rx="1"/></svg>
    Dashboard
  </a>
  <a class="nav-item" routerLink="/productos" routerLinkActive="active">
    <svg viewBox="0 0 24 24"><path d="M5 8h14M5 8a2 2 0 110-4h14a2 2 0 110 4M5 8l1 12a2 2 0 002 2h8a2 2 0 002-2L19 8"/></svg>
    Productos
  </a>
  <a class="nav-item" *ngIf="puede('Administrador', 'Almacenero')" routerLink="/movimientos" routerLinkActive="active">
    <svg viewBox="0 0 24 24"><path d="M7 16V4m0 0L3 8m4-4l4 4M17 8v12m0 0l4-4m-4 4l-4-4"/></svg>
    Movimientos
  </a>
  <a class="nav-item" *ngIf="puede('Administrador', 'Almacenero')" routerLink="/alertas" routerLinkActive="active">
    <svg viewBox="0 0 24 24"><path d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V4a2 2 0 10-4 0v1.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9"/></svg>
    Alertas
  </a>

  <ng-container *ngIf="puede('Administrador', 'Vendedor', 'Gerencia')">
    <span class="nav-section">Ventas</span>
    <a class="nav-item" routerLink="/ventas" routerLinkActive="active">
      <svg viewBox="0 0 24 24"><path d="M3 3h2l.4 2M7 13h10l4-8H5.4M7 13L5.4 5M7 13l-2.293 2.293c-.63.63-.184 1.707.707 1.707H17m0 0a2 2 0 100 4 2 2 0 000-4zm-8 2a2 2 0 11-4 0 2 2 0 014 0z"/></svg>
      Ventas
    </a>
  </ng-container>

  <ng-container *ngIf="puede('Administrador', 'Almacenero')">
  <span class="nav-section">Compras</span>
  <a class="nav-item" routerLink="/proveedores" routerLinkActive="active">
    <svg viewBox="0 0 24 24"><path d="M1 3h15v13H1zM16 8h4l3 3v5h-7zM5.5 21a2.5 2.5 0 100-5 2.5 2.5 0 000 5zM18.5 21a2.5 2.5 0 100-5 2.5 2.5 0 000 5z"/></svg>
    Proveedores
  </a>
  <a class="nav-item" routerLink="/compras" routerLinkActive="active">
    <svg viewBox="0 0 24 24"><path d="M16 4h2a2 2 0 012 2v14a2 2 0 01-2 2H6a2 2 0 01-2-2V6a2 2 0 012-2h2M9 2h6a1 1 0 011 1v2a1 1 0 01-1 1H9a1 1 0 01-1-1V3a1 1 0 011-1zM9 12h6M9 16h6"/></svg>
    Órdenes de compra
  </a>

  </ng-container>

  <ng-container *ngIf="puede('Administrador', 'Almacenero', 'Gerencia')">
  <span class="nav-section">Inteligencia</span>
  <a class="nav-item" routerLink="/predicciones" routerLinkActive="active">
    <svg viewBox="0 0 24 24"><path d="M3 3v18h18M7 15l4-4 3 3 6-7M17 7h3v3"/></svg>
    Predicciones
  </a>
  <a class="nav-item" *ngIf="puede('Administrador', 'Gerencia')" routerLink="/recomendaciones" routerLinkActive="active">
    <svg viewBox="0 0 24 24"><path d="M9 18h6M10 22h4M12 2a7 7 0 00-4 12.7c.6.5 1 1.2 1 2V17h6v-.3c0-.8.4-1.5 1-2A7 7 0 0012 2z"/></svg>
    Recomendaciones
  </a>
  </ng-container>

  <ng-container *ngIf="puede('Administrador', 'Almacenero', 'Gerencia')">
  <span class="nav-section">Reportes</span>
  <a class="nav-item" routerLink="/reportes/stock" routerLinkActive="active">
    <svg viewBox="0 0 24 24"><path d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z"/></svg>
    Stock actual
  </a>
  <a class="nav-item" routerLink="/reportes/historial" routerLinkActive="active">
    <svg viewBox="0 0 24 24"><path d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"/></svg>
    Historial
  </a>
  <a class="nav-item" *ngIf="puede('Administrador', 'Gerencia')" routerLink="/reportes/rotacion" routerLinkActive="active">
    <svg viewBox="0 0 24 24"><path d="M4 4v5h5M20 20v-5h-5M5.6 15A8 8 0 0018.4 17M18.4 9A8 8 0 005.6 7"/></svg>
    Rotación
  </a>
  <a class="nav-item" *ngIf="puede('Administrador', 'Gerencia')" routerLink="/reportes/comparacion" routerLinkActive="active">
    <svg viewBox="0 0 24 24"><path d="M3 3v18h18M7 14l4-4 3 3 5-6"/></svg>
    Comparación mensual
  </a>

  </ng-container>

  <ng-container *ngIf="puede('Administrador')">
  <span class="nav-section">Administración</span>
  <a class="nav-item" routerLink="/usuarios" routerLinkActive="active">
    <svg viewBox="0 0 24 24"><path d="M17 21v-2a4 4 0 00-4-4H5a4 4 0 00-4 4v2M9 11a4 4 0 100-8 4 4 0 000 8z"/></svg>
    Usuarios
  </a>
  <a class="nav-item" routerLink="/categorias" routerLinkActive="active">
    <svg viewBox="0 0 24 24"><path d="M7 7h.01M7 3h5a1.99 1.99 0 011.414.586l7 7a2 2 0 010 2.828l-5 5a2 2 0 01-2.828 0l-7-7A2 2 0 013 12V7a4 4 0 014-4z"/></svg>
    Categorías
  </a>
  </ng-container>

  <div class="sidebar-footer">
    <div class="user-row">
      <div class="avatar">{{ iniciales }}</div>
      <div>
        <div class="user-name">{{ usuario?.nombre }}</div>
        <div class="user-role">{{ usuario?.rol }}</div>
      </div>
    </div>
    <button class="btn-logout" (click)="logout()">
      <svg viewBox="0 0 24 24"><path d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1"/></svg>
      Cerrar sesión
    </button>
  </div>
</aside>

<div class="main">
  <router-outlet />
</div>
  `
})
export class ShellComponent implements OnInit {
  private auth   = inject(AuthService);
  private router = inject(Router);
  private cdr    = inject(ChangeDetectorRef);

  // Se leen cada vez para reflejar el rol vigente (puede cambiar mientras hay sesión)
  get usuario() { return this.auth.getUser(); }
  get iniciales(): string {
    return this.usuario?.nombre
      ?.split(' ').map((n: string) => n[0]).join('').substring(0, 2).toUpperCase() ?? 'U';
  }

  ngOnInit() {
    // Si el administrador cambió mi rol, se aplica sin esperar a un nuevo login
    const rolAntes = this.auth.getUser()?.rol;
    this.auth.refrescarPerfil().subscribe({
      next: p => {
        // Solo se reacciona si el servidor devolvió un rol válido y distinto al guardado
        if (p?.rol && p.rol !== rolAntes) this.router.navigateByUrl(this.auth.rutaInicio());
        this.cdr.detectChanges();   // redibuja el menú con el rol vigente
      }
    });
  }

  puede(...roles: string[]): boolean { return this.auth.tieneRol(...roles); }

  logout() { this.auth.logout(); }
}
