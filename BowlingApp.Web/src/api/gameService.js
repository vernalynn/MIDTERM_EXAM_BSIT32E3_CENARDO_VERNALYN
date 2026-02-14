// =============================================================================================
// STUDENT TODO: Implement API Calls
// =============================================================================================
// The frontend is currently using Mock data. Your task is to implement the actual API calls
// to your .NET Backend.
//
// 1. Ensure your .NET Backend is running (e.g., http://localhost:5000).
// 2. Use fetch() or axios to make HTTP requests.
// 3. Update the matching 'VITE_APP_MODE' in your .env file to 'LIVE' to test this.

const API_BASE_URL = "http://localhost:5000/api/game"; // Update port if necessary

export const createGame = async (playerNames) => {
    // TODO: Call POST /api/game with playerNames
    // Example:
    const response = await fetch(API_BASE_URL, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(playerNames)
    });
    return await response.json();
};

export const getGame = async (gameId) => {
    // TODO: Call GET /api/game/{gameId}
    // Example:
    const response = await fetch(`${API_BASE_URL}/${gameId}`);
    return await response.json();
};

export const rollBall = async (gameId, playerId, pins) => {
    // TODO: Call POST /api/game/{gameId}/roll
    // Body: { playerId, pins }
    // Example:
    const response = await fetch(`${API_BASE_URL}/${gameId}/roll`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ playerId, pins })
    });
    // Backend returns 200 OK with no body, so we don't need to parse JSON
    return response.ok;
};
