import { Outlet, createRootRoute } from "@tanstack/react-router";

export const Route = createRootRoute({
  component: RootComponent,
});

function RootComponent() {
  return (
    <div className="h-screen w-full bg-gray-900 text-gray-100 flex items-center justify-center p-4">
      <div className="w-full max-w-6xl h-full flex flex-col shadow-2xl rounded-lg overflow-hidden">
        <Outlet />
      </div>
    </div>
  );
}
