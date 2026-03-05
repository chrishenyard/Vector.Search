import { useState } from "react";
import { Link, Outlet, createRootRoute } from "@tanstack/react-router";

export const Route = createRootRoute({
  component: RootComponent,
});

interface NavLink {
  to: string;
  label: string;
}

const navLinks: NavLink[] = [
  { to: "/", label: "Home" },
  { to: "/embeddings", label: "Embeddings" },
  { to: "/code-search", label: "Code Search" },
  // Add more links here as needed
];

function RootComponent() {
  const [isCollapsed, setIsCollapsed] = useState<boolean>(false);

  return (
    <div style={{ display: "flex", minHeight: "100vh", margin: 0 }}>
      {/* Sidebar */}
      <aside
        style={{
          width: isCollapsed ? "64px" : "220px",
          transition: "width 0.2s ease",
          background: "#101828",
          color: "#fff",
          padding: "16px",
          boxSizing: "border-box",
          display: "flex",
          flexDirection: "column",
        }}
      >
        <button
          style={{
            width: "100%",
            padding: "8px",
            marginBottom: "24px",
            border: "1px solid #475467",
            borderRadius: "6px",
            background: "#1d2939",
            color: "#fff",
            cursor: "pointer",
            fontWeight: "bold",
          }}
          onClick={() => setIsCollapsed((prev) => !prev)}
          aria-label={isCollapsed ? "Expand sidebar" : "Collapse sidebar"}
        >
          {isCollapsed ? "▶" : "◀ Collapse"}
        </button>
        <nav style={{ flex: 1 }}>
          <ul
            style={{
              listStyle: "none",
              padding: 0,
              margin: 0,
              display: "flex",
              flexDirection: "column",
              gap: "8px",
            }}
          >
            {navLinks.map((link) => (
              <li key={link.to}>
                <Link
                  to={link.to}
                  style={{
                    display: "block",
                    padding: "10px 12px",
                    borderRadius: "6px",
                    color: "#eaecf0",
                    textDecoration: "none",
                    background: "transparent",
                    transition: "background 0.2s",
                    whiteSpace: "nowrap",
                    overflow: "hidden",
                    textOverflow: "ellipsis",
                  }}
                  activeProps={{
                    style: {
                      background: "#344054",
                      fontWeight: "bold",
                      color: "#fff",
                    },
                  }}
                >
                  {isCollapsed ? link.label.charAt(0) : link.label}
                </Link>
              </li>
            ))}
          </ul>
        </nav>
      </aside>

      {/* Main Content Area */}
      <main
        style={{
          flex: 1,
          padding: "32px",
          background: "#000000q",
          boxSizing: "border-box",
        }}
      >
        <Outlet />
      </main>
    </div>
  );
}
