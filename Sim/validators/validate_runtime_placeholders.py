PLANNED_SYSTEMS = [
    "network_sessions",
    "dedicated_server_wrapper",
    "multi_world_host",
]


def validate_runtime_placeholders(repo_root):
    return {
        "status": "UNKNOWN",
        "passed": 0,
        "failed": 0,
        "unknown": len(PLANNED_SYSTEMS),
        "notes": ["Planned runtime module not implemented yet: " + name for name in PLANNED_SYSTEMS],
    }
