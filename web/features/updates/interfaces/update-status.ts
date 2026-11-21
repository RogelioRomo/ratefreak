export interface UpdateStatus {
  installed: {version:string;revision:string;official:boolean};
  enabled:boolean;
  state:'disabled'|'unchecked'|'checking'|'current'|'available'|'unavailable'|'no-release'|'development';
  latestVersion:string|null;
  releaseUrl:string|null;
  lastAttemptAt:string|null;
  lastSuccessAt:string|null;
  nextCheckAt:string|null;
  stale:boolean;
  canCheck:boolean;
}
