from pydantic import BaseModel
from typing import Dict, List, Literal, Optional


# AI tuning shared across games. ``difficulty`` controls how close to optimal the
# AI plays; ``style`` shifts its risk appetite (cautious vs. aggressive).
Difficulty = Literal["easy", "medium", "hard"]
PlayStyle = Literal["safe", "balanced", "risky"]


class PilesState(BaseModel):
    ascending1: int
    ascending2: int
    descending1: int
    descending2: int


class LastMovePlay(BaseModel):
    card: int
    pileSlot: int


class LastMove(BaseModel):
    playerUsername: str
    plays: List[LastMovePlay]


class AIMoveRequest(BaseModel):
    hand: List[int]
    piles: PilesState
    drawPileCount: int
    minCardsThisTurn: int
    playedCards: Optional[List[int]] = None   # all cards placed on piles so far
    moveHistory: Optional[List[LastMove]] = None  # every move played this game
    difficulty: Difficulty = "medium"
    style: PlayStyle = "balanced"


class CardPlay(BaseModel):
    card: int
    pileSlot: int  # 0=ascending1, 1=ascending2, 2=descending1, 3=descending2


class AIMoveResponse(BaseModel):
    plays: List[CardPlay]
    source: str  # "claude" or "fallback"


class AIMessageRequest(BaseModel):
    playerUsername: str
    hand: List[int]
    piles: PilesState
    drawPileCount: int
    moveHistory: Optional[List[LastMove]] = None
    difficulty: Difficulty = "medium"
    style: PlayStyle = "balanced"


class AIMessageResponse(BaseModel):
    message: str


# ── Flip 7 ────────────────────────────────────────────────────────────────────
# Press-your-luck: each turn an active player chooses Hit (take another card) or
# Stay (bank the line). The deck is skewed (higher numbers more common), so the
# bust risk of hitting rises with how many number values you already hold.

class Flip7Opponent(BaseModel):
    numberCount: int            # how many number cards are in their line
    roundScore: int             # what they'd bank if they stayed now
    status: str                 # active | stayed | frozen | busted
    cumulativeScore: int = 0


class Flip7AIMoveRequest(BaseModel):
    myNumbers: List[int]                 # distinct number values currently in my line
    myModifiers: List[str] = []          # e.g. ["x2", "+4"]
    hasSecondChance: bool = False
    myRoundScore: int = 0                # what I'd bank if I stay now
    myCumulativeScore: int = 0
    targetScore: int = 200
    # Composition of what can still be drawn (number value -> remaining copies)
    # plus the total cards left in the draw pile (all card types).
    deckRemaining: Dict[int, int] = {}
    drawPileCount: int = 0
    opponents: List[Flip7Opponent] = []
    difficulty: Difficulty = "medium"
    style: PlayStyle = "balanced"


class Flip7AIMoveResponse(BaseModel):
    action: Literal["hit", "stay"]
    source: str                          # "claude" or "fallback"
    bustProbability: float = 0.0
