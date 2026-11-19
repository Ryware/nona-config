/* @refresh reload */
import { render } from "@solidjs/web";

import App from "./app/App";
import "./index.css";

const root = document.getElementById("root");

if (!root) {
  throw new Error("Root element not found");
}

render(() => <App />, root);
