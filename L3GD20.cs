
using System;

namespace mcutils
{
    public class L3GD20
    {
        public static int WHO_AM_I		=	0x0F;
        public static int CTRL_REG1		=	0x20;
        public static int CTRL_REG2		=	0x21;
        public static int CTRL_REG3		=	0x22;
        public static int CTRL_REG4		=	0x23;
        public static int CTRL_REG5		=	0x24;
        public static int REFERENCE		=	0x25;
        public static int OUT_TEMP		=	0x26;
        public static int STATUS_REG	=	0x27;
        public static int OUT_X_L		=	0x28;
        public static int OUT_X_H		=	0x29;
        public static int OUT_Y_L		=	0x2A;
        public static int OUT_Y_H		=	0x2B;
        public static int OUT_Z_L		=	0x2C;
        public static int OUT_Z_H		=	0x2D;
        public static int FIFO_CTRL_REG	=	0x2E;
        public static int FIFO_SRC_REG	=	0x2F;
        public static int INT1_CFG		=	0x30;
        public static int INT1_SRC		=	0x31;
        public static int INT1_THS_XH	=	0x32;
        public static int INT1_THS_XL	=	0x33;
        public static int INT1_THS_YH	=	0x34;
        public static int INT1_THS_YL	=	0x35;
        public static int INT1_THS_ZH	=	0x36;
        public static int INT1_THS_ZL	=	0x37;
        public static int INT1_DURATION	=	0x38;
    }
}
