import { Outlet, createRootRoute } from '@tanstack/react-router';

export const Route = createRootRoute({
    component: RootComponent
});

function RootComponent() {
    return (
        <>
            <h1>Vector Search</h1>
            <Outlet />
        </>
    );
}