import {defineConfig} from '@playwright/test';
export default defineConfig({testDir:'.',timeout:30_000,use:{baseURL:'http://127.0.0.1:4173',locale:'fa-IR'},webServer:{command:'npm --prefix ../../frontend run dev -- --host 127.0.0.1 --port 4173',url:'http://127.0.0.1:4173',reuseExistingServer:true,timeout:60_000}});
