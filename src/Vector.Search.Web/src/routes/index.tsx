import { createFileRoute } from "@tanstack/react-router";

export const Route = createFileRoute("/")({
    component: RouteComponent
});

function RouteComponent() {
    return (
        <div>
            <h2>Home</h2>
            <p>Welcome to the Vector Search application!</p>
        </div>
    );
}
