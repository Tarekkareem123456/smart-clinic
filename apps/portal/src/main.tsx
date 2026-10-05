import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { appName, applyDocumentDirection } from "@smart-clinic/shared";

export const surface = "portal" as const;

applyDocumentDirection("ar");

function App() {
  return (
    <StrictMode>
      <main>
        <h1>{appName}</h1>
        <p>{surface}</p>
      </main>
    </StrictMode>
  );
}

const rootElement = document.getElementById("root");

if (!rootElement) {
  throw new Error("Portal application root element was not found.");
}

createRoot(rootElement).render(<App />);