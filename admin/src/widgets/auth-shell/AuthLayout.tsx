import { type ParentComponent } from "solid-js";
import { ThemeToggle } from "../../shared/ui/ThemeToggle";

export const AuthLayout: ParentComponent = (props) => {
  return (
    <div class="relative flex min-h-screen flex-col items-stretch justify-start overflow-hidden bg-background p-0 sm:items-center sm:justify-center sm:p-4">
      <ThemeToggle class="absolute right-4 top-4 z-20" />

      {/* Ambient background glow orbs */}
      <div class="pointer-events-none absolute top-[-18%] left-[-24%] h-[46%] w-[70%] rounded-full bg-primary/10 blur-[120px] sm:top-[-20%] sm:left-[-20%] sm:h-[60%] sm:w-[60%] sm:blur-[150px]" />
      <div class="pointer-events-none absolute right-[-28%] bottom-[-14%] h-[38%] w-[70%] rounded-full bg-primary-container/5 blur-[120px] sm:right-[-20%] sm:bottom-[-20%] sm:h-[60%] sm:w-[60%] sm:blur-[150px]" />
      
      <div class="relative z-10 flex min-h-screen w-full flex-col items-stretch justify-start sm:min-h-0 sm:items-center sm:justify-center">
        {props.children}
      </div>
    </div>
  );
};
