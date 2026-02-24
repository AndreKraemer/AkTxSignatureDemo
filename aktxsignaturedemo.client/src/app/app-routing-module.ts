import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { HomeComponent } from './pages/home/home';
import { EditorComponent } from './pages/editor/editor';
import { ViewerComponent } from './pages/viewer/viewer';
import { SignComponent } from './pages/sign/sign';

const routes: Routes = [
  { path: '', component: HomeComponent },
  { path: 'editor', component: EditorComponent },
  { path: 'viewer', component: ViewerComponent },
  { path: 'sign', component: SignComponent },
  { path: '**', redirectTo: '' },
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule],
})
export class AppRoutingModule {}
