import {lazy,Suspense} from "react";
const SimulationValidation=lazy(()=>import("../features/match3d/SimulationValidation"));
import { createRoot } from "react-dom/client";
import { QueryClientProvider } from "@tanstack/react-query";
import { BrowserRouter } from "react-router-dom";
import App from "./main";
import { queryClient } from "../services/queryClient";
createRoot(document.getElementById("root")!).render(<QueryClientProvider client={queryClient}><BrowserRouter>{import.meta.env.DEV && new URLSearchParams(location.search).get("simulationTest")==="v11" ? <Suspense fallback={<p>Carregando validação…</p>}><SimulationValidation/></Suspense> : <App/>}</BrowserRouter></QueryClientProvider>);
