from pydantic import BaseModel
from typing import List, Optional


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


class CardPlay(BaseModel):
    card: int
    pileSlot: int  # 0=ascending1, 1=ascending2, 2=descending1, 3=descending2


class AIMoveResponse(BaseModel):
    plays: List[CardPlay]
    source: str  # "claude" or "fallback"
