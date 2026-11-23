import {lazy,Suspense} from "react";
const SimulationValidation=lazy(()=>import("../features/match3d/SimulationValidation"));
import { createRoot } from "react-dom/client";
import { QueryClientProvider } from "@tanstack/react-query";
import { BrowserRouter } from "react-router-dom";
import App from "./main";
import { queryClient } from "../services/queryClient";
createRoot(document.getElementById("root")!).render(<QueryClientProvider client={queryClient}><BrowserRouter>{import.meta.env.DEV && ["v11","v12","v13","v14"].includes(new URLSearchParams(location.search).get("simulationTest")??"") ? <Suspense fallback={<p>Carregando validação…</p>}><SimulationValidation/></Suspense> : <App/>}</BrowserRouter></QueryClientProvider>);
