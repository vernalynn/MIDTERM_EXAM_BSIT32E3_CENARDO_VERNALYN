using BowlingApp.API.Data;
using BowlingApp.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BowlingApp.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GameController : ControllerBase
    {
        private readonly BowlingContext _context;

        public GameController(BowlingContext context)
        {
            _context = context;
        }

        // POST: api/Game
        // Create a new game with players
        [HttpPost]
        public async Task<ActionResult<Game>> CreateGame([FromBody] List<string> playerNames)
        {
            // STUDENT TODO:
            // 1. Create a new Game entity
            var game = new Game
            {
                IsFinished = false,
                DatePlayed = DateTime.Now
            };

            _context.Games.Add(game);
            await _context.SaveChangesAsync(); // Save to get game ID
            // 2. Create Player entities for each name provided.
            foreach (var name in playerNames)
            {
                var player = new Player
                {
                    Name = name,
                    GameId = game.Id
                };

                _context.Players.Add(player);
                await _context.SaveChangesAsync(); // Save to get player ID
                                                   // 3. (Optional) Initialize 10 empty Frames for each player to simplify logic?
                for (int i = 1; i <= 10; i++)
                {
                    var frame = new Frame
                    {
                        FrameNumber = i,
                        PlayerId = player.Id,
                        Roll1 = null,
                        Roll2 = null,
                        Roll3 = null,
                        Score = null
                    };
                    _context.Frames.Add(frame);
                }
            }
            // 4. Save to Database using _context.
            await _context.SaveChangesAsync();
            // 5. Return the created Game with Players
            var createdGame = await _context.Games
                .Include(g => g.Players)
                .FirstOrDefaultAsync(g => g.Id == game.Id);

            return Ok(new
            {
                id = createdGame.Id,
                players = createdGame.Players.Select(p => new
                {
                    id = p.Id,
                    name = p.Name
                })
            });
        }

        // GET: api/Game/5
        // Get game details and current scores
        [HttpGet("{id}")]
        public async Task<ActionResult<Game>> GetGame(int id)
        {
            // STUDENT TODO:
            // 1. Find the Game by ID.
            // 2. Include Players and Frames in the query (use .Include()).
            var game = await _context.Games
              .Include(g => g.Players)
                  .ThenInclude(p => p.Frames)
              .FirstOrDefaultAsync(g => g.Id == id);

            if (game == null)
            {
                return NotFound();
            }
            // 3. Return the Game.
            var response = new
            {
                id = game.Id,
                isFinished = game.IsFinished,
                players = game.Players.Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                    frames = p.Frames.OrderBy(f => f.FrameNumber).Select(f => new
                    {
                        frameNumber = f.FrameNumber,
                        rolls = GetRollsList(f),
                        score = f.Score
                    }).ToList()
                }).ToList()
            };

            return Ok(response);
        }

        // POST: api/Game/5/roll
        // Record a roll for a specific player
        [HttpPost("{gameId}/roll")]
        public async Task<IActionResult> Roll(int gameId, [FromBody] RollRequest request)
        {
            // STUDENT TODO:
            // 1. Find the Player and Game.
            var player = await _context.Players
                .Include(p => p.Frames)
                .FirstOrDefaultAsync(p => p.Id == request.PlayerId && p.GameId == gameId);

            if (player == null)
            {
                return NotFound("Player not found");
            }
            // 2. Determine the Current Frame for the player (the first incompletion frame).
            var currentFrame = player.Frames
                .OrderBy(f => f.FrameNumber)
                .FirstOrDefault(f => IsFrameIncomplete(f));

            if (currentFrame == null)
            {
                return BadRequest("All frames are complete");
            }
            // 3. Update the Frame with the rolled pins (Roll1, Roll2, or Roll3).
            if (currentFrame.Roll1 == null)
            {
                currentFrame.Roll1 = request.Pins;
            }
            else if (currentFrame.Roll2 == null)
            {
                currentFrame.Roll2 = request.Pins;
            }
            else if (currentFrame.Roll3 == null && currentFrame.FrameNumber == 10)
            {
                currentFrame.Roll3 = request.Pins;
            }
            // 4. BOWLING LOGIC:
            //    - Check for Strikes (10 on 1st roll) -> Mark frame as Strike.
            //    - Check for Spares (Total 10 on 2 rolls) -> Mark frame as Spare.
            // 5. SCORING CALCULATION:
            //    - Update the score for the current frame.
            //    - CRITICAL: Check *previous* frames. If they were strikes/spares, they might need this new roll to calculate their final score!
            // 6. Save changes to Database.
            CalculateAllScores(player);

            await _context.SaveChangesAsync();

            return Ok();
        }

        // HELPER METHODS

        // Check if a frame is incomplete (needs more rolls)
        private bool IsFrameIncomplete(Frame frame)
        {
            if (frame.FrameNumber < 10)
            {
                // Frames 1-9
                if (frame.Roll1 == null) return true; // Need first roll
                if (frame.Roll1 == 10) return false; // Strike, done
                if (frame.Roll2 == null) return true; // Need second roll
                return false; // Both rolls done
            }
            else
            {
                // Frame 10 - special handling
                if (frame.Roll1 == null) return true;
                if (frame.Roll2 == null) return true;

                // If strike or spare in 10th, need 3rd roll
                if (frame.Roll1 == 10 || (frame.Roll1 + frame.Roll2 == 10))
                {
                    return frame.Roll3 == null;
                }

                return false; // Open frame, only 2 rolls
            }
        }

        // Get list of rolls for a frame (for API response)
        private List<int> GetRollsList(Frame frame)
        {
            var rolls = new List<int>();
            if (frame.Roll1.HasValue) rolls.Add(frame.Roll1.Value);
            if (frame.Roll2.HasValue) rolls.Add(frame.Roll2.Value);
            if (frame.Roll3.HasValue) rolls.Add(frame.Roll3.Value);
            return rolls;
        }

        //  CRITICAL: Calculate all scores for a player
        private void CalculateAllScores(Player player)
        {
            var frames = player.Frames.OrderBy(f => f.FrameNumber).ToList();
            int cumulativeScore = 0;

            for (int i = 0; i < frames.Count; i++)
            {
                var frame = frames[i];
                int? frameScore = CalculateFrameScore(frames, i);

                if (frameScore.HasValue)
                {
                    cumulativeScore += frameScore.Value;
                    frame.Score = cumulativeScore;
                }
                else
                {
                    frame.Score = null; // Can't calculate yet (waiting for future rolls)
                }
            }
        }

        // Calculate score for a single frame
        private int? CalculateFrameScore(List<Frame> frames, int frameIndex)
        {
            var frame = frames[frameIndex];

            if (frame.FrameNumber < 10)
            {
                // Frames 1-9
                if (frame.Roll1 == 10) // Strike
                {
                    // Score = 10 + next 2 rolls
                    var next2 = GetNext2Rolls(frames, frameIndex);
                    if (next2 == null) return null; // Can't calculate yet
                    return 10 + next2.Value;
                }
                else if (frame.Roll1 + frame.Roll2 == 10) // Spare
                {
                    // Score = 10 + next 1 roll
                    var next1 = GetNext1Roll(frames, frameIndex);
                    if (next1 == null) return null; // Can't calculate yet
                    return 10 + next1.Value;
                }
                else // Open frame
                {
                    // Score = sum of both rolls
                    if (frame.Roll1.HasValue && frame.Roll2.HasValue)
                    {
                        return frame.Roll1.Value + frame.Roll2.Value;
                    }
                    return null; // Not complete yet
                }
            }
            else
            {
                // Frame 10 - just sum all rolls
                int total = 0;
                if (frame.Roll1.HasValue) total += frame.Roll1.Value;
                if (frame.Roll2.HasValue) total += frame.Roll2.Value;
                if (frame.Roll3.HasValue) total += frame.Roll3.Value;

                // Only return score if frame is complete
                if (!IsFrameIncomplete(frame))
                {
                    return total;
                }
                return null;
            }
        }

        // Get next 2 rolls (for strike scoring)
        private int? GetNext2Rolls(List<Frame> frames, int currentIndex)
        {
            if (currentIndex + 1 >= frames.Count) return null;

            var nextFrame = frames[currentIndex + 1];

            if (nextFrame.Roll1 == null) return null;

            if (nextFrame.Roll1 == 10 && nextFrame.FrameNumber < 10)
            {
                // Next frame is also a strike
                if (currentIndex + 2 < frames.Count)
                {
                    var frameAfterNext = frames[currentIndex + 2];
                    if (frameAfterNext.Roll1 == null) return null;
                    return 10 + frameAfterNext.Roll1.Value;
                }
                // Or it's the 10th frame
                if (nextFrame.FrameNumber == 10)
                {
                    if (nextFrame.Roll2 == null) return null;
                    return 10 + nextFrame.Roll2.Value;
                }
                return null;
            }
            else
            {
                // Next frame has 2 rolls
                if (nextFrame.Roll2 == null) return null;
                return nextFrame.Roll1.Value + nextFrame.Roll2.Value;
            }
        }

        // Get next 1 roll (for spare scoring)
        private int? GetNext1Roll(List<Frame> frames, int currentIndex)
        {
            if (currentIndex + 1 >= frames.Count) return null;

            var nextFrame = frames[currentIndex + 1];
            return nextFrame.Roll1;
        }
    }

    public class RollRequest
    {
        public int PlayerId { get; set; }
        public int Pins { get; set; }
    }
}